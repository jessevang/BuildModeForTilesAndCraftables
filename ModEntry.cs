using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using Microsoft.Xna.Framework.Input;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace BuildModeForTilesAndCraftables
{
    public class ModConfig
    {
        public Keys TurnOnBuildMode { get; set; } = Keys.F1;
        public Keys ToggleBetweenAddandRemoveTiles { get; set; } = Keys.Space;
    }

    public class ModEntry : Mod
    {
        public ModConfig Config { get; private set; }
        public static ModEntry Instance { get; private set; }
        // Whether our custom build mode is active.
        private bool isBuildModeActive = false;
        // Whether the player is currently dragging.
        private bool isDragging = false;
        // Drag start and end in world tile coordinates.
        private Point dragStart;
        private Point dragEnd;
        // Toggle for removal mode (true) vs. placement mode (false).
        private bool removeMode = false;
        // Base tile size in pixels (64 if the game’s base size is 64).
        private const int TileSize = 64;
        private Point originalViewport;
        private Vector2 buildCameraOffset = Vector2.Zero;

        // Used so that drag point is captured on click.
        private Point? dragViewport = null;

        // Store the mod helper for polling input.
        private IModHelper modHelper;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>() ?? new ModConfig();
            this.modHelper = helper;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
        }

        /// <summary>
        /// Returns a Rectangle representing the toolbar area.
        /// Adjust the toolbarYOffset value manually until the rectangle is moved down to align with your toolbar.
        /// </summary>
        private Rectangle GetToolbarBounds()
        {
            // Get the actual screen size, independent of zoom.
            int screenWidth = Game1.viewport.Width;  // Using viewport instead of uiViewport to ensure screen consistency.
            int screenHeight = Game1.viewport.Height;

            // UI elements don't scale with zoom, so we do NOT scale offsets by zoom.
            int toolbarXOffset = (int)(screenWidth* 0.16403);       // Left offset.
            int toolbarHeight = 180;        // Toolbar height.
            int toolbarYOffset = (int)(screenHeight*0.1875);       // Vertical offset (move down).



            // Log the initial viewport dimensions for debugging.
            //Instance.Monitor.Log($"Viewport Dimensions -> Width: {screenWidth.ToString()}, Height: {screenHeight.ToString()}", StardewModdingAPI.LogLevel.Debug);

            // Ensure the toolbar area stays in the correct position even when zooming.
            Rectangle toolbarBounds = new Rectangle(
                toolbarXOffset,
                screenHeight - toolbarYOffset,
                screenWidth,
                toolbarHeight
            );

            // Log the calculated toolbar bounds.
            //Instance.Monitor.Log($"Toolbar Bounds -> X: {toolbarBounds.X.ToString()}, Y: {toolbarBounds.Y.ToString()}, Width: {toolbarBounds.Width.ToString()}, Height: {toolbarBounds.Height.ToString()}", StardewModdingAPI.LogLevel.Debug);

            return toolbarBounds;
        }








        /// <summary>
        /// Processes input events.
        /// </summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Use our toolbar bounds.
            Rectangle toolbarBounds = GetToolbarBounds();
            Point mousePoint = new Point(Game1.getMouseX(false), Game1.getMouseY(false));

            // If the click occurs inside the toolbar area, do not process build mode input.
            if (toolbarBounds.Contains(mousePoint))
                return;

            // Toggle custom build mode.
            if (e.Button == Config.TurnOnBuildMode.ToSButton())
            {
                isBuildModeActive = !isBuildModeActive;
                if (isBuildModeActive)
                {
                    Game1.player.canMove = false;  // Freeze the farmer.
                    originalViewport = new Point(Game1.viewport.X, Game1.viewport.Y);
                    buildCameraOffset = Vector2.Zero;
                }
                else
                {
                    isDragging = false;
                    Game1.player.canMove = true;   // Unfreeze the farmer.
                }
                return;
            }

            if (!isBuildModeActive)
                return;

            // Toggle placement/removal mode using the configured key.
            if (e.Button == Config.ToggleBetweenAddandRemoveTiles.ToSButton())
            {
                removeMode = !removeMode;
            }

            // Process left-click (outside the toolbar area).
            if (e.Button == SButton.MouseLeft)
            {
                if (!isDragging)
                {
                    isDragging = true;
                    // Lock in the viewport when dragging begins.
                    dragViewport = new Point(Game1.viewport.X, Game1.viewport.Y);
                    dragStart = SnapToTileWorld(mousePoint, dragViewport);
                    dragEnd = dragStart;
                }
                else
                {
                    // Use the locked viewport while ending the drag.
                    dragEnd = SnapToTileWorld(mousePoint, dragViewport);
                    Rectangle selection = GetTileSelectionRectangle(dragStart, dragEnd);
                    if (removeMode)
                        RemoveTiles(selection);
                    else
                        PlaceTiles(selection);
                    isDragging = false;
                    dragViewport = null; // Clear the locked viewport once done.
                }
            }

            // Right-click cancels the drag selection.
            if (e.Button == SButton.MouseRight)
            {
                if (isDragging)
                {
                    isDragging = false;
                }
            }
        }

        /// <summary>
        /// Called every tick. Updates the drag selection and, if in build mode, scrolls the viewport with WASD.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // Update drag selection if dragging.
            if (isBuildModeActive && isDragging && dragViewport.HasValue)
            {
                dragEnd = SnapToTileWorld(new Point(Game1.getMouseX(false), Game1.getMouseY(false)), dragViewport);
            }

            // While in build mode, update the buildCameraOffset based on WASD input.
            if (isBuildModeActive)
            {
                KeyboardState keyboardState = Keyboard.GetState();
                int scrollSpeed = 12; // Adjust scroll speed as needed.

                if (keyboardState.IsKeyDown(Keys.W))
                    buildCameraOffset.Y -= scrollSpeed;
                if (keyboardState.IsKeyDown(Keys.S))
                    buildCameraOffset.Y += scrollSpeed;
                if (keyboardState.IsKeyDown(Keys.A))
                    buildCameraOffset.X -= scrollSpeed;
                if (keyboardState.IsKeyDown(Keys.D))
                    buildCameraOffset.X += scrollSpeed;

                Game1.viewport.X = originalViewport.X + (int)buildCameraOffset.X;
                Game1.viewport.Y = originalViewport.Y + (int)buildCameraOffset.Y;
            }
        }

        /// <summary>
        /// Draws overlay UI for selection and instructions.
        /// </summary>
        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!isBuildModeActive)
                return;

            SpriteBatch spriteBatch = e.SpriteBatch;
            Point mousePoint = new Point(Game1.getMouseX(false), Game1.getMouseY(false));
            Rectangle toolbarBounds = GetToolbarBounds();

            // Only draw the build mode highlight if the mouse is not over the toolbar.
            if (!toolbarBounds.Contains(mousePoint))
            {
                if (!isDragging)
                {
                    Point hoverTile = SnapToTileWorld(mousePoint);
                    DrawTileOverlay(spriteBatch, hoverTile.X, hoverTile.Y);
                }
                else
                {
                    Rectangle selection = GetTileSelectionRectangle(dragStart, dragEnd);
                    for (int x = selection.X; x < selection.X + selection.Width; x++)
                    {
                        for (int y = selection.Y; y < selection.Y + selection.Height; y++)
                        {
                            DrawTileOverlay(spriteBatch, x, y);
                        }
                    }
                }
            }

            // Draw custom instructions overlay regardless of mouse position.
            Rectangle dialogBox = new Rectangle(10, 10, 1000, 150);
            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.mouseCursors,
                new Rectangle(403, 373, 6, 6),
                dialogBox.X, dialogBox.Y, dialogBox.Width, dialogBox.Height,
                Color.White * 0.5f, 4f, false
            );

            string modeText = removeMode
                ? $"Removal Mode ({Config.ToggleBetweenAddandRemoveTiles}: Placement Mode)"
                : $"Placement Mode ({Config.ToggleBetweenAddandRemoveTiles}: Removal Mode)";
            SpriteText.drawString(spriteBatch, modeText, dialogBox.X + 10, dialogBox.Y + 10);
            SpriteText.drawString(spriteBatch, $"LMB: Select/Drag | RMB: Cancel | {Config.TurnOnBuildMode}: Exit", dialogBox.X + 20, dialogBox.Y + 60);
        }

        /// <summary>
        /// Converts a screen coordinate to a world tile coordinate.
        /// </summary>
        private Point SnapToTileWorld(Point screenPoint, Point? viewportOverride = null)
        {
            int offsetX = viewportOverride?.X ?? Game1.viewport.X;
            int offsetY = viewportOverride?.Y ?? Game1.viewport.Y;
            int worldX = screenPoint.X + offsetX;
            int worldY = screenPoint.Y + offsetY;
            int tileX = worldX / TileSize;
            int tileY = worldY / TileSize;
            return new Point(tileX, tileY);
        }

        /// <summary>
        /// Returns a rectangle representing the selection area in world tile coordinates.
        /// </summary>
        private Rectangle GetTileSelectionRectangle(Point startTile, Point endTile)
        {
            int x = Math.Min(startTile.X, endTile.X);
            int y = Math.Min(startTile.Y, endTile.Y);
            int width = Math.Abs(startTile.X - endTile.X) + 1;
            int height = Math.Abs(startTile.Y - endTile.Y) + 1;
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Draws a tile overlay at the given world tile coordinates.
        /// </summary>
        private void DrawTileOverlay(SpriteBatch spriteBatch, int tileX, int tileY)
        {
            Vector2 tileVector = new Vector2(tileX, tileY);
            Color fillColor = Color.Green * 0.4f;
            if (removeMode)
            {
                bool hasObject = Game1.currentLocation.objects.ContainsKey(tileVector);
                bool hasFloorFeature = Game1.currentLocation.terrainFeatures != null &&
                    Game1.currentLocation.terrainFeatures.ContainsKey(tileVector) &&
                    Game1.currentLocation.terrainFeatures[tileVector].ToString().Contains("Floor", StringComparison.OrdinalIgnoreCase);
                if (!hasObject && !hasFloorFeature)
                    fillColor = Color.Red * 0.4f;
            }
            else
            {
                bool canPlace = Game1.currentLocation.isTilePlaceable(tileVector, false);
                if (!canPlace)
                    fillColor = Color.Red * 0.4f;
            }
            int screenX = (int)(tileX * TileSize * Game1.options.zoomLevel) - (int)(Game1.viewport.X * Game1.options.zoomLevel);
            int screenY = (int)(tileY * TileSize * Game1.options.zoomLevel) - (int)(Game1.viewport.Y * Game1.options.zoomLevel);
            int drawTileSize = (int)(TileSize * Game1.options.zoomLevel);
            Rectangle tileRect = new Rectangle(screenX, screenY, drawTileSize, drawTileSize);
            spriteBatch.Draw(Game1.staminaRect, tileRect, fillColor);
            DrawRectangleOutline(spriteBatch, tileRect, Color.Black * 0.2f, 2);
        }

        /// <summary>
        /// Draws an outline around a rectangle.
        /// </summary>
        private void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        }

        /// <summary>
        /// Called when a selection is confirmed in placement mode.
        /// Attempts to place the currently held floor item or craftable into each grid block in the selection.
        /// </summary>
        private void PlaceTiles(Rectangle tileArea)
        {
            var currentItem = Game1.player.CurrentItem as StardewValley.Object;
            if (currentItem == null)
            {
                return;
            }

            Dictionary<string, string> floorLookup = Flooring.GetFloorPathItemLookup();
            bool isFloor = (currentItem.Category == -20 || floorLookup.ContainsKey(currentItem.ParentSheetIndex.ToString()));

            if (!isFloor && !currentItem.bigCraftable.Value)
            {
                return;
            }

            if (isFloor)
            {
                int itemWidth = currentItem.bigCraftable.Value ? 2 : 1;
                int itemHeight = currentItem.bigCraftable.Value ? 2 : 1;
                int availableRows = tileArea.Height / itemHeight;
                int availableCols = tileArea.Width / itemWidth;
                int totalPlacementsPossible = availableRows * availableCols;
                int availableInInventory = currentItem.Stack;
                int placementsToDo = Math.Min(totalPlacementsPossible, availableInInventory);
                int placedCount = 0;
                if (Game1.currentLocation.terrainFeatures == null)
                {
                    Game1.currentLocation.terrainFeatures.Set(new Dictionary<Vector2, TerrainFeature>());
                }
                for (int row = 0; row < availableRows; row++)
                {
                    for (int col = 0; col < availableCols; col++)
                    {
                        if (placedCount >= placementsToDo)
                            break;
                        int startX = tileArea.X + col * itemWidth;
                        int startY = tileArea.Y + row * itemHeight;
                        bool canPlaceBlock = true;
                        for (int dx = 0; dx < itemWidth; dx++)
                        {
                            for (int dy = 0; dy < itemHeight; dy++)
                            {
                                Vector2 tile = new Vector2(startX + dx, startY + dy);
                                if (Game1.currentLocation.objects.ContainsKey(tile) ||
                                    (Game1.currentLocation.terrainFeatures != null && Game1.currentLocation.terrainFeatures.ContainsKey(tile)) ||
                                    !Game1.currentLocation.isTilePlaceable(tile, false))
                                {
                                    canPlaceBlock = false;
                                    break;
                                }
                            }
                            if (!canPlaceBlock)
                                break;
                        }
                        if (!canPlaceBlock)
                            continue;
                        string floorId;
                        if (!floorLookup.TryGetValue(currentItem.ParentSheetIndex.ToString(), out floorId))
                        {
                            floorId = currentItem.ParentSheetIndex.ToString();
                        }
                        Flooring newFloor = new Flooring(floorId);
                        Vector2 placementTile = new Vector2(startX, startY);
                        if (!Game1.currentLocation.terrainFeatures.ContainsKey(placementTile))
                        {
                            Game1.currentLocation.terrainFeatures.Add(placementTile, newFloor);
                            placedCount++;
                        }
                    }
                    if (placedCount >= placementsToDo)
                        break;
                }
                if (placedCount > 0)
                {
                    currentItem.Stack -= placedCount;
                    if (currentItem.Stack <= 0)
                        Game1.player.removeItemFromInventory(currentItem);
                }
            }
            else
            {
                bool isChest = currentItem is StardewValley.Objects.Chest || currentItem.Name.ToLower().Contains("chest");
                bool isFence = currentItem.Name.ToLower().Contains("fence");
                bool isGate = currentItem.Name.ToLower().Contains("gate");

                if (isChest)
                {
                    return;
                }
                if (isFence)
                {
                    return;
                }
                if (isGate)
                {
                    return;
                }

                int itemWidth = 1;
                int itemHeight = 1;
                int availableRows = tileArea.Height / itemHeight;
                int availableCols = tileArea.Width / itemWidth;
                int totalPlacementsPossible = availableRows * availableCols;
                int availableInInventory = currentItem.Stack;
                int placementsToDo = Math.Min(totalPlacementsPossible, availableInInventory);
                int placedCount = 0;
                for (int row = 0; row < availableRows; row++)
                {
                    for (int col = 0; col < availableCols; col++)
                    {
                        if (placedCount >= placementsToDo)
                            break;
                        int x = tileArea.X + col * itemWidth;
                        int y = tileArea.Y + row * itemHeight;
                        Vector2 tileCoord = new Vector2(x, y);

                        if (!Game1.currentLocation.isTilePlaceable(tileCoord, false))
                            continue;

                        StardewValley.Object placedItem = currentItem.getOne() as StardewValley.Object;
                        if (placedItem == null)
                        {
                            continue;
                        }
                        placedItem.Stack = 1;
                        bool success = placedItem.placementAction(Game1.currentLocation, (int)tileCoord.X, (int)tileCoord.Y, Game1.player);
                        if (success)
                        {
                            if (!Game1.currentLocation.objects.ContainsKey(tileCoord))
                            {
                                Game1.currentLocation.objects.Add(tileCoord, placedItem);
                            }
                            placedCount++;
                        }
                    }
                    if (placedCount >= placementsToDo)
                        break;
                }
                if (placedCount > 0)
                {
                    currentItem.Stack -= placedCount;
                    if (currentItem.Stack <= 0)
                        Game1.player.removeItemFromInventory(currentItem);
                }
            }
        }

        /// <summary>
        /// Called when a selection is confirmed in removal mode.
        /// </summary>
        private void RemoveTiles(Rectangle tileArea)
        {
            for (int x = tileArea.X; x < tileArea.X + tileArea.Width; x++)
            {
                for (int y = tileArea.Y; y < tileArea.Y + tileArea.Height; y++)
                {
                    Vector2 tile = new Vector2(x, y);
                    if (Game1.currentLocation.objects.ContainsKey(tile))
                    {
                        StardewValley.Object obj = Game1.currentLocation.objects[tile];

                        if (obj is StardewValley.Objects.Chest ||
                            (!string.IsNullOrEmpty(obj.Name) && obj.Name.ToLower().Contains("chest")))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(obj.Name) && obj.Name.ToLower().Contains("floor"))
                        {
                            Dictionary<string, string> floorLookup = Flooring.GetFloorPathItemLookup();
                            string floorTileId;
                            if (!floorLookup.TryGetValue(obj.ParentSheetIndex.ToString(), out floorTileId))
                            {
                                floorTileId = obj.ParentSheetIndex.ToString();
                            }
                            StardewValley.Object floorTileItem = new StardewValley.Object(
                                itemId: floorTileId,
                                initialStack: obj.Stack,
                                isRecipe: false,
                                price: obj.Price,
                                quality: obj.Quality
                            );
                            bool addedFloor = Game1.player.addItemToInventoryBool(floorTileItem);
                            if (addedFloor)
                            {
                                Game1.currentLocation.objects.Remove(tile);
                            }
                        }
                        else
                        {
                            bool isFenceOrGate = !string.IsNullOrEmpty(obj.Name) &&
                                                 (obj.Name.ToLower().Contains("fence") || obj.Name.ToLower().Contains("gate"));

                            if (!obj.bigCraftable.Value && !isFenceOrGate)
                            {
                                continue;
                            }

                            bool added = Game1.player.addItemToInventoryBool(obj);
                            if (added)
                            {
                                Game1.currentLocation.objects.Remove(tile);
                            }
                        }
                    }
                }
            }
            RemoveFloorTiles(tileArea);
        }

        /// <summary>
        /// Removes floor tiles from terrainFeatures in the selected area.
        /// </summary>
        private void RemoveFloorTiles(Rectangle tileArea)
        {
            if (Game1.currentLocation.terrainFeatures == null)
                return;

            List<Vector2> keys = new List<Vector2>(Game1.currentLocation.terrainFeatures.Keys);
            foreach (Vector2 key in keys)
            {
                if (key.X >= tileArea.X && key.X < tileArea.X + tileArea.Width &&
                    key.Y >= tileArea.Y && key.Y < tileArea.Y + tileArea.Height)
                {
                    var feature = Game1.currentLocation.terrainFeatures[key];
                    if (feature != null && feature.ToString().Contains("Floor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (feature is Flooring floor)
                        {
                            string floorType = floor.GetData().ItemId.ToString();
                            StardewValley.Object floorTileItem = new StardewValley.Object(
                                itemId: floorType,
                                initialStack: 1,
                                isRecipe: false,
                                price: 0,
                                quality: 0
                            );
                            bool added = Game1.player.addItemToInventoryBool(floorTileItem);
                            if (added)
                            {
                                Game1.currentLocation.terrainFeatures.Remove(key);
                            }
                        }
                    }
                }
            }
        }
    }
}
