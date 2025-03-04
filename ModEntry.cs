using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.FloorsAndPaths;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using xTile.Tiles;
using Microsoft.Xna.Framework.Input;
using StardewValley.Objects;

namespace BuildModeForTilesAndCraftables
{
    public class ModEntry : Mod
    {
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
        // EffectiveTileSize for drawing – scales with zoom.
        private int EffectiveTileSize => (int)(TileSize * Game1.options.zoomLevel);
        private Point originalViewport;
        private Vector2 buildCameraOffset = Vector2.Zero;

        // Used so that drag point is captured on click.
        private Point? dragViewport = null;

        // Store the mod helper for polling input.
        private IModHelper modHelper;

        public override void Entry(IModHelper helper)
        {
            this.modHelper = helper;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedHud += this.OnRenderedHud;
        }

        /// <summary>
        /// Processes input events.
        /// </summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Toggle custom build mode using Z.
            if (e.Button == SButton.Z)
            {
                isBuildModeActive = !isBuildModeActive;
                if (isBuildModeActive)
                {
                    Monitor.Log("Custom Build Mode Activated", LogLevel.Info);
                    Game1.player.canMove = false;  // Freeze the farmer.
                    originalViewport = new Point(Game1.viewport.X, Game1.viewport.Y);
                    buildCameraOffset = Vector2.Zero;
                }
                else
                {
                    Monitor.Log("Custom Build Mode Deactivated", LogLevel.Info);
                    isDragging = false;
                    Game1.player.canMove = true;   // Unfreeze the farmer.
                }
                return;
            }

            if (!isBuildModeActive)
                return;

            // Toggle placement/removal mode using B.
            if (e.Button == SButton.B)
            {
                removeMode = !removeMode;
                Monitor.Log($"Switched to {(removeMode ? "Removal" : "Placement")} Mode", LogLevel.Info);
            }

            // Left-click: start or finish a drag selection.
            if (e.Button == SButton.MouseLeft)
            {
                if (!isDragging)
                {
                    isDragging = true;
                    // Lock in the viewport when dragging begins.
                    dragViewport = new Point(Game1.viewport.X, Game1.viewport.Y);
                    dragStart = SnapToTileWorld(new Point(Game1.getMouseX(false), Game1.getMouseY(false)), dragViewport);
                    dragEnd = dragStart;
                }
                else
                {
                    // Use the locked viewport while ending the drag.
                    dragEnd = SnapToTileWorld(new Point(Game1.getMouseX(false), Game1.getMouseY(false)), dragViewport);
                    Rectangle selection = GetTileSelectionRectangle(dragStart, dragEnd);
                    if (removeMode)
                        RemoveTiles(selection);
                    else
                        PlaceTiles(selection);
                    isDragging = false;
                    dragViewport = null; // clear the locked viewport once done
                }
            }

            // Right-click cancels the drag selection.
            if (e.Button == SButton.MouseRight)
            {
                if (isDragging)
                {
                    isDragging = false;
                    Monitor.Log("Selection cancelled", LogLevel.Info);
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

            if (!isDragging)
            {
                Point hoverTile = SnapToTileWorld(new Point(Game1.getMouseX(false), Game1.getMouseY(false)));
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

            Rectangle dialogBox = new Rectangle(10, 10, 1000, 150);
            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.mouseCursors,
                new Rectangle(403, 373, 6, 6),
                dialogBox.X, dialogBox.Y, dialogBox.Width, dialogBox.Height,
                Color.White * 0.5f, 4f, false
            );
            string modeText = removeMode ? "Removal Mode (B:Placement Mode)" : "Placement Mode (B:Removal Mode)";
            SpriteText.drawString(spriteBatch, modeText, dialogBox.X + 10, dialogBox.Y + 10);
            SpriteText.drawString(spriteBatch, "LMB: Drag/Select | RMB: Cancel | F1: Exit Build Mode",
                                   dialogBox.X + 20, dialogBox.Y + 60);
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

        using StardewValley.Objects; // Needed to check for Chest type

private void PlaceTiles(Rectangle tileArea)
    {
        Monitor.Log($"Placing tiles/craftables in tile area: {tileArea}", LogLevel.Info);

        // Get the currently held item.
        var currentItem = Game1.player.CurrentItem as StardewValley.Object;
        if (currentItem == null)
        {
            Monitor.Log("Current item is not a valid StardewValley.Object. Aborting placement.", LogLevel.Info);
            return;
        }

        // Get the floor lookup dictionary.
        Dictionary<string, string> floorLookup = Flooring.GetFloorPathItemLookup();
        // Determine if the current item should be treated as a floor tile.
        bool isFloor = (currentItem.Category == -20 || floorLookup.ContainsKey(currentItem.ParentSheetIndex.ToString()));

        if (isFloor)
        {
            // Floor items: use normal footprint (1x1 for normal; 2x2 for big craftable floors)
            int itemWidth = currentItem.bigCraftable.Value ? 2 : 1;
            int itemHeight = currentItem.bigCraftable.Value ? 2 : 1;

            int availableRows = tileArea.Height / itemHeight;
            int availableCols = tileArea.Width / itemWidth;
            int totalPlacementsPossible = availableRows * availableCols;
            int availableInInventory = currentItem.Stack;
            int placementsToDo = Math.Min(totalPlacementsPossible, availableInInventory);
            Monitor.Log($"(Floor) Attempting to place {placementsToDo} instances (of {totalPlacementsPossible} possible and {availableInInventory} available).", LogLevel.Info);

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
                Monitor.Log($"(Floor) Placed {placedCount} items. Remaining in inventory: {currentItem.Stack}", LogLevel.Info);
                if (currentItem.Stack <= 0)
                    Game1.player.removeItemFromInventory(currentItem);
            }
            else
            {
                Monitor.Log("No valid floor placements found in the selected area.", LogLevel.Info);
            }
        }
        else
        {
            // For non-floor objects:
            // Normally, big craftables use a 2x2 footprint.
            // We want to reduce that footprint by half (2x2 becomes 1x1, 4x4 becomes 2x2), except for items whose logic depends on their default size.
            // For example, chests (of type Chest) and fences (identified by name) are kept at default.
            bool isChest = currentItem is Chest;
            bool isFence = currentItem.Name.IndexOf("fence", StringComparison.OrdinalIgnoreCase) >= 0;

            int defaultFootprint = currentItem.bigCraftable.Value ? 2 : 1;
            // If the item is a big craftable and not a chest or fence, reduce footprint.
            int reducedFootprint = (currentItem.bigCraftable.Value && !isChest && !isFence) ? defaultFootprint / 2 : defaultFootprint;
            int itemWidth = currentItem.bigCraftable.Value ? reducedFootprint : 1;
            int itemHeight = currentItem.bigCraftable.Value ? reducedFootprint : 1;

            int availableRows = tileArea.Height / itemHeight;
            int availableCols = tileArea.Width / itemWidth;
            int totalPlacementsPossible = availableRows * availableCols;
            int availableInInventory = currentItem.Stack;
            int placementsToDo = Math.Min(totalPlacementsPossible, availableInInventory);
            Monitor.Log($"(Object) Attempting to place {placementsToDo} instances (of {totalPlacementsPossible} possible and {availableInInventory} available).", LogLevel.Info);

            int placedCount = 0;
            for (int row = 0; row < availableRows; row++)
            {
                for (int col = 0; col < availableCols; col++)
                {
                    if (placedCount >= placementsToDo)
                        break;

                    int x = tileArea.X + col * itemWidth;
                    int y = tileArea.Y + row * itemHeight;
                    Vector2 placementTile = new Vector2(x, y);
                    if (Game1.currentLocation.isTilePlaceable(placementTile, false) && !Game1.currentLocation.objects.ContainsKey(placementTile))
                    {
                        StardewValley.Object placedItem = currentItem.getOne() as StardewValley.Object;
                        if (placedItem == null)
                        {
                            Monitor.Log("Failed to clone the current item using getOne().", LogLevel.Error);
                            continue;
                        }
                        placedItem.Stack = 1; // set the placed item count

                        Vector2 placementCoord;
                        // For big craftables (that are not chests or fences), adjust placement coordinate.
                        if (placedItem.bigCraftable.Value && !isChest && !isFence)
                        {
                            placementCoord = new Vector2(placementTile.X, placementTile.Y - reducedFootprint);
                            placedItem.placementAction(Game1.currentLocation, (int)placementCoord.X, (int)placementCoord.Y, Game1.player);
                        }
                        else
                        {
                            placementCoord = placementTile;
                            placedItem.placementAction(Game1.currentLocation, (int)placementCoord.X, (int)placementCoord.Y, Game1.player);
                        }

                        // Use the same coordinate for adding the object.
                        Game1.currentLocation.objects.Add(placementCoord, placedItem);
                        placedCount++;
                    }
                }
                if (placedCount >= placementsToDo)
                    break;
            }

            if (placedCount > 0)
            {
                currentItem.Stack -= placedCount;
                Monitor.Log($"(Object) Placed {placedCount} items. Remaining in inventory: {currentItem.Stack}", LogLevel.Info);
                if (currentItem.Stack <= 0)
                    Game1.player.removeItemFromInventory(currentItem);
            }
            else
            {
                Monitor.Log("No valid object placements found in the selected area.", LogLevel.Info);
            }
        }
    }


    /// <summary>
    /// Called when a selection is confirmed in removal mode.
    /// </summary>
    private void RemoveTiles(Rectangle tileArea)
        {
            Monitor.Log($"Removing tiles/craftables in tile area: {tileArea}", LogLevel.Info);
            for (int x = tileArea.X; x < tileArea.X + tileArea.Width; x++)
            {
                for (int y = tileArea.Y; y < tileArea.Y + tileArea.Height; y++)
                {
                    Vector2 tile = new Vector2(x, y);
                    if (Game1.currentLocation.objects.ContainsKey(tile))
                    {
                        StardewValley.Object obj = Game1.currentLocation.objects[tile];
                        if (!string.IsNullOrEmpty(obj.Name) && obj.Name.Contains("Floor", StringComparison.OrdinalIgnoreCase))
                        {
                            Monitor.Log($"Tile is considered a Floor: {obj.Name}", LogLevel.Info);
                            Dictionary<string, string> floorLookup = Flooring.GetFloorPathItemLookup();
                            string floorTileId;
                            if (!floorLookup.TryGetValue(obj.ItemId, out floorTileId))
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
                                Monitor.Log($"Removed floor tile '{obj.Name}' from tile {tile}, added inventory item with id {floorTileId}.", LogLevel.Info);
                            }
                            else
                            {
                                Monitor.Log($"Could not add floor tile '{obj.Name}' to inventory", LogLevel.Info);
                            }
                        }
                        else
                        {
                            bool added = Game1.player.addItemToInventoryBool(obj);
                            if (added)
                            {
                                Game1.currentLocation.objects.Remove(tile);
                                Monitor.Log($"Removed {obj.Name} from tile {tile}", LogLevel.Info);
                            }
                            else
                            {
                                Monitor.Log($"Could not add {obj.Name} to inventory", LogLevel.Info);
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
                            Monitor.Log($"FLOOR TYPE in this area {key} is identified as item id {floorType}.", LogLevel.Info);
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
                                Monitor.Log($"Removed floor feature at {key} and added inventory item.", LogLevel.Info);
                            }
                            else
                            {
                                Monitor.Log($"Could not add floor feature inventory item for feature at {key}", LogLevel.Info);
                            }
                        }
                    }
                }
            }
        }
    }
}
