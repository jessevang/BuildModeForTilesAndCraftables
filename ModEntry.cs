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
using xTile.Tiles;
using StardewModdingAPI.Utilities;

namespace BuildModeForTilesAndCraftables
{
    public class ModConfig
    {
        public Keys TurnOnBuildMode { get; set; } = Keys.F1;
        public string note000 { get; set; } = "View mode cycles between build and replace mode and allows user to click around in build mode without selecting anything. Generally used to view or to leverage other mods";
        public bool EnableViewMode { get; set; } = false;
        public Keys ToggleBetweenAddandRemoveTiles { get; set; } = Keys.Space;
        public bool SelectionRemovesFloorTiles { get; set; } = true;
        public bool SelectionRemovesBigCraftables { get; set; } = true;
        //public bool selectionRemoveNonFloorNonBigCraftableItems { get; set; } = true;

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
        // Toggle  removal mode vs. placement mode vs. View Mode 
        enum BuildMode
        {
            Placement,
            Removal,
            View
        }

   

        // In your class, store the current mode:
        private BuildMode currentMode = BuildMode.Placement;

        // A method to cycle through the modes:
        private void CycleBuildMode()
        {
            if (Config.EnableViewMode)
            {
                // Cycle through all modes: Placement, Removal, View.
                int modeCount = Enum.GetValues(typeof(BuildMode)).Length;
                currentMode = (BuildMode)(((int)currentMode + 1) % modeCount);
            }
            else
            {
                // Only toggle between Placement and Removal
                currentMode = currentMode == BuildMode.Placement ? BuildMode.Removal : BuildMode.Placement;
            }
        }

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
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        }


        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {

            //Uses API for Generic Mod Config Menu and creates the config menu
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            // Register Mod in GMCM
            gmcm.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            // Add Config Options
            gmcm.AddKeybind(
                ModManifest,
                name: () => "Build Mode",
                tooltip: () => "Toggles On/Off Build Mode",
                getValue: () => Config.TurnOnBuildMode.ToSButton(),
                setValue: value => Config.TurnOnBuildMode = (Keys)value
            );

            gmcm.AddKeybind(
                ModManifest,
                name: () => "Selection / Removal",
                tooltip: () => "Once in build mode this hotkey toggles between Selection mode and Removal Mode",
                getValue: () => Config.ToggleBetweenAddandRemoveTiles.ToSButton(),
                setValue: value => Config.ToggleBetweenAddandRemoveTiles = (Keys)value
                );

            gmcm.AddBoolOption(
                    ModManifest,
                    name: () => "Remove Floor Tiles?",
                    tooltip: () => "Remove mode now removes Floor Tiles like wood planks",
                    getValue: () => Config.SelectionRemovesFloorTiles,
                    setValue: value => Config.SelectionRemovesFloorTiles = value
                );


            gmcm.AddBoolOption(
                ModManifest,
                name: () => "Remove Big Craftables?",
                tooltip: () => "Remove Mode will allow removal of Craftables like Kegs.",
                getValue: () => Config.SelectionRemovesBigCraftables,
                setValue: value => Config.SelectionRemovesBigCraftables = value
            );

        }






        /// <summary>
        /// Returns a Rectangle representing the toolbar area.
        /// </summary>
        private Rectangle GetToolbarBounds()
        {

            int screenWidth = Game1.viewport.Width;
            int screenHeight = Game1.viewport.Height;

            int toolbarXOffset = (int)(screenWidth * 0.16403);
            int toolbarHeight = 180;
            int toolbarYOffset = (int)(screenHeight * 0.1475);


            // Instance.Monitor.Log($"Viewport Dimensions -> Width: {screenWidth.ToString()}, Height: {screenHeight.ToString()}", StardewModdingAPI.LogLevel.Debug);


            /// <param name="x">The x coordinate of the top-left corner of the created <see cref="Rectangle"/>.</param>
            /// <param name="y">The y coordinate of the top-left corner of the created <see cref="Rectangle"/>.</param>
            /// <param name="width">The width of the created <see cref="Rectangle"/>.</param>
            /// <param name="height">The height of the created <see cref="Rectangle"/>.</param>
            // Ensure the toolbar area stays in the correct position even when zooming.
            Rectangle toolbarBounds = new Rectangle(
                x: toolbarXOffset,
                y: screenHeight - toolbarYOffset,
                width: (int)(screenWidth * .65),
                height: toolbarHeight
            );

            // Instance.Monitor.Log($"Toolbar Bounds -> X: {toolbarBounds.X.ToString()}, Y: {toolbarBounds.Y.ToString()}, Width: {toolbarBounds.Width.ToString()}, Height: {toolbarBounds.Height.ToString()}", StardewModdingAPI.LogLevel.Debug);

            return toolbarBounds;
        }



        /// <summary>
        /// Processes input events.
        /// </summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Toggle placement/removal mode using the configured key.
            if (e.Button == Config.ToggleBetweenAddandRemoveTiles.ToSButton())
            {

                CycleBuildMode();
            }
            
            if (currentMode == BuildMode.View)
            {
                return;
            }

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
                    if (currentMode == BuildMode.Removal)
                    {

                        if (Config.SelectionRemovesBigCraftables)
                        {
                            RemoveTiles(selection);
                        }
                        if (Config.SelectionRemovesFloorTiles)
                        {
                            RemoveFloorTiles(selection);
                        }


                        /*
                        if (Config.selectionRemoveNonFloorNonBigCraftableItems)
                        {
                            removeAllbutFloorAndBigCraftables(selection);
                        }
                        */



                    }





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


            // Draw custom instructions overlay regardless of mouse position.
            Rectangle dialogBox = new Rectangle(10, 10, 1000, 150);
            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.mouseCursors,
                new Rectangle(403, 373, 6, 6),
                dialogBox.X, dialogBox.Y, dialogBox.Width, dialogBox.Height,
                Color.White * 0.5f, 4f, false
            );



            string modeText = currentMode switch
            {
                BuildMode.Placement => $"Placement Mode ({Config.ToggleBetweenAddandRemoveTiles}: toggle mode)",
                BuildMode.Removal => $"Removal Mode ({Config.ToggleBetweenAddandRemoveTiles}: toggle mode)",
                BuildMode.View => $"View Mode ({Config.ToggleBetweenAddandRemoveTiles}: toggle mode)",
                _ => ""
            };

            SpriteText.drawString(spriteBatch, modeText, dialogBox.X + 10, dialogBox.Y + 10);
            SpriteText.drawString(spriteBatch, $"LMB: Select/Drag | RMB: Cancel | {Config.TurnOnBuildMode}: Exit", dialogBox.X + 20, dialogBox.Y + 60);


            //removes tile on mouse if in view mode
            if (currentMode == BuildMode.View)
            {
                return;
            }

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
            // When in remove mode, default to yellow; otherwise default to green.

            Color fillColor = currentMode switch
            {
                BuildMode.Placement => Color.Green * 0.6f,
                BuildMode.Removal => Color.Yellow * 0.6f,
                BuildMode.View => Color.Green * 0.6f,
                _ => Color.Green * 0.6f
            };

            if (currentMode == BuildMode.Removal)
            {
                // Check if there is something to remove.
                bool hasObject = Game1.currentLocation.objects.ContainsKey(tileVector);
                bool hasFloorFeature = Game1.currentLocation.terrainFeatures != null &&
                    Game1.currentLocation.terrainFeatures.ContainsKey(tileVector) &&
                    Game1.currentLocation.terrainFeatures[tileVector].ToString().Contains("Floor", StringComparison.OrdinalIgnoreCase);
                // If nothing is present for removal, show red.
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
                                bool objectPresent = Game1.currentLocation.objects.ContainsKey(tile);
                                // Block if there is already flooring in the tile.
                                if (Game1.currentLocation.terrainFeatures != null && Game1.currentLocation.terrainFeatures.ContainsKey(tile))
                                {
                                    canPlaceBlock = false;
                                    break;
                                }
                                // If no object is present, the tile must be placeable.
                                if (!objectPresent && !Game1.currentLocation.isTilePlaceable(tile, false))
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
                        // Even if an object is on the tile, if there's no floor yet then add the flooring.
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
                        /*
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
                        */
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



        /// <summary>
        /// Called when a selection is confirmed in removal mode.
        /// Removes all that aren't chest, floor, or Big Craftable.
        /// </summary>
        private void removeAllbutFloorAndBigCraftables(Rectangle tileArea)
        {
            for (int x = tileArea.X; x < tileArea.X + tileArea.Width; x++)
            {
                for (int y = tileArea.Y; y < tileArea.Y + tileArea.Height; y++)
                {
                    Vector2 tile = new Vector2(x, y);
                    if (Game1.currentLocation.objects.ContainsKey(tile))
                    {
                        StardewValley.Object obj = Game1.currentLocation.objects[tile];

                        // Ensure chests are not removed.
                        bool isChest = obj is StardewValley.Objects.Chest ||
                                       (!string.IsNullOrEmpty(obj.Name) && obj.Name.ToLower().Contains("chest"));
                        if (isChest)
                        {
                            continue;
                        }

                        // Determine if this object is a floor tile (by name) or a big craftable.
                        bool isFloor = !string.IsNullOrEmpty(obj.Name) && obj.Name.ToLower().Contains("floor");
                        bool isBigCraftable = obj.bigCraftable.Value;

                        // If the object is either a floor tile or a big craftable, do not remove it.
                        if (isFloor || isBigCraftable)
                        {
                            continue;
                        }

                        // Otherwise, attempt to add the object to the player's inventory.
                        bool added = Game1.player.addItemToInventoryBool(obj);
                        if (added)
                        {
                            Game1.currentLocation.objects.Remove(tile);
                        }
                    }
                }
            }
        }


    }
}


public interface IGenericModConfigMenuApi
{
    /*********
    ** Methods
    *********/
    /****
    ** Must be called first
    ****/
    /// <summary>Register a mod whose config can be edited through the UI.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="reset">Reset the mod's config to its default values.</param>
    /// <param name="save">Save the mod's current config to the <c>config.json</c> file.</param>
    /// <param name="titleScreenOnly">Whether the options can only be edited from the title screen.</param>
    /// <remarks>Each mod can only be registered once, unless it's deleted via <see cref="Unregister"/> before calling this again.</remarks>
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);


    /****
    ** Basic options
    ****/
    /// <summary>Add a section title at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="text">The title text shown in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the title, or <c>null</c> to disable the tooltip.</param>
    void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);

    /// <summary>Add a paragraph of text at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="text">The paragraph text to display.</param>
    void AddParagraph(IManifest mod, Func<string> text);

    /// <summary>Add an image at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="texture">The image texture to display.</param>
    /// <param name="texturePixelArea">The pixel area within the texture to display, or <c>null</c> to show the entire image.</param>
    /// <param name="scale">The zoom factor to apply to the image.</param>
    void AddImage(IManifest mod, Func<Texture2D> texture, Rectangle? texturePixelArea = null, int scale = Game1.pixelZoom);

    /// <summary>Add a boolean option at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="getValue">Get the current value from the mod config.</param>
    /// <param name="setValue">Set a new value in the mod config.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);

    /// <summary>Add an integer option at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="getValue">Get the current value from the mod config.</param>
    /// <param name="setValue">Set a new value in the mod config.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="min">The minimum allowed value, or <c>null</c> to allow any.</param>
    /// <param name="max">The maximum allowed value, or <c>null</c> to allow any.</param>
    /// <param name="interval">The interval of values that can be selected.</param>
    /// <param name="formatValue">Get the display text to show for a value, or <c>null</c> to show the number as-is.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string> formatValue = null, string fieldId = null);

    /// <summary>Add a float option at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="getValue">Get the current value from the mod config.</param>
    /// <param name="setValue">Set a new value in the mod config.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="min">The minimum allowed value, or <c>null</c> to allow any.</param>
    /// <param name="max">The maximum allowed value, or <c>null</c> to allow any.</param>
    /// <param name="interval">The interval of values that can be selected.</param>
    /// <param name="formatValue">Get the display text to show for a value, or <c>null</c> to show the number as-is.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string> tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string> formatValue = null, string fieldId = null);

    /// <summary>Add a string option at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="getValue">Get the current value from the mod config.</param>
    /// <param name="setValue">Set a new value in the mod config.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="allowedValues">The values that can be selected, or <c>null</c> to allow any.</param>
    /// <param name="formatAllowedValue">Get the display text to show for a value from <paramref name="allowedValues"/>, or <c>null</c> to show the values as-is.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);

    /// <summary>Add a key binding at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="getValue">Get the current value from the mod config.</param>
    /// <param name="setValue">Set a new value in the mod config.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);

    /// <summary>Add a key binding list at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="getValue">Get the current value from the mod config.</param>
    /// <param name="setValue">Set a new value in the mod config.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);


    /****
    ** Multi-page management
    ****/
    /// <summary>Start a new page in the mod's config UI, or switch to that page if it already exists. All options registered after this will be part of that page.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="pageId">The unique page ID.</param>
    /// <param name="pageTitle">The page title shown in its UI, or <c>null</c> to show the <paramref name="pageId"/> value.</param>
    /// <remarks>You must also call <see cref="AddPageLink"/> to make the page accessible. This is only needed to set up a multi-page config UI. If you don't call this method, all options will be part of the mod's main config UI instead.</remarks>
    void AddPage(IManifest mod, string pageId, Func<string> pageTitle = null);

    /// <summary>Add a link to a page added via <see cref="AddPage"/> at the current position in the form.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="pageId">The unique ID of the page to open when the link is clicked.</param>
    /// <param name="text">The link text shown in the form.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the link, or <c>null</c> to disable the tooltip.</param>
    void AddPageLink(IManifest mod, string pageId, Func<string> text, Func<string> tooltip = null);


    /****
    ** Advanced
    ****/
    /// <summary>Add an option at the current position in the form using custom rendering logic.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="name">The label text to show in the form.</param>
    /// <param name="draw">Draw the option in the config UI. This is called with the sprite batch being rendered and the pixel position at which to start drawing.</param>
    /// <param name="tooltip">The tooltip text shown when the cursor hovers on the field, or <c>null</c> to disable the tooltip.</param>
    /// <param name="beforeMenuOpened">A callback raised just before the menu containing this option is opened.</param>
    /// <param name="beforeSave">A callback raised before the form's current values are saved to the config (i.e. before the <c>save</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="afterSave">A callback raised after the form's current values are saved to the config (i.e. after the <c>save</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="beforeReset">A callback raised before the form is reset to its default values (i.e. before the <c>reset</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="afterReset">A callback raised after the form is reset to its default values (i.e. after the <c>reset</c> callback passed to <see cref="Register"/>).</param>
    /// <param name="beforeMenuClosed">A callback raised just before the menu containing this option is closed.</param>
    /// <param name="height">The pixel height to allocate for the option in the form, or <c>null</c> for a standard input-sized option. This is called and cached each time the form is opened.</param>
    /// <param name="fieldId">The unique field ID for use with <see cref="OnFieldChanged"/>, or <c>null</c> to auto-generate a randomized ID.</param>
    /// <remarks>The custom logic represented by the callback parameters is responsible for managing its own state if needed. For example, you can store state in a static field or use closures to use a state variable.</remarks>
    void AddComplexOption(IManifest mod, Func<string> name, Action<SpriteBatch, Vector2> draw, Func<string> tooltip = null, Action beforeMenuOpened = null, Action beforeSave = null, Action afterSave = null, Action beforeReset = null, Action afterReset = null, Action beforeMenuClosed = null, Func<int> height = null, string fieldId = null);

    /// <summary>Set whether the options registered after this point can only be edited from the title screen.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="titleScreenOnly">Whether the options can only be edited from the title screen.</param>
    /// <remarks>This lets you have different values per-field. Most mods should just set it once in <see cref="Register"/>.</remarks>
    void SetTitleScreenOnlyForNextOptions(IManifest mod, bool titleScreenOnly);

    /// <summary>Register a method to notify when any option registered by this mod is edited through the config UI.</summary>
    /// <param name="mod">The mod's manifest.</param>
    /// <param name="onChange">The method to call with the option's unique field ID and new value.</param>
    /// <remarks>Options use a randomized ID by default; you'll likely want to specify the <c>fieldId</c> argument when adding options if you use this.</remarks>
    void OnFieldChanged(IManifest mod, Action<string, object> onChange);

    /// <summary>Open the config UI for a specific mod.</summary>
    /// <param name="mod">The mod's manifest.</param>
    void OpenModMenu(IManifest mod);

    /// <summary>Open the config UI for a specific mod, as a child menu if there is an existing menu.</summary>
    /// <param name="mod">The mod's manifest.</param>
    void OpenModMenuAsChildMenu(IManifest mod);

    /// <summary>Get the currently-displayed mod config menu, if any.</summary>
    /// <param name="mod">The manifest of the mod whose config menu is being shown, or <c>null</c> if not applicable.</param>
    /// <param name="page">The page ID being shown for the current config menu, or <c>null</c> if not applicable. This may be <c>null</c> even if a mod config menu is shown (e.g. because the mod doesn't have pages).</param>
    /// <returns>Returns whether a mod config menu is being shown.</returns>
    bool TryGetCurrentMenu(out IManifest mod, out string page);

    /// <summary>Remove a mod from the config UI and delete all its options and pages.</summary>
    /// <param name="mod">The mod's manifest.</param>
    void Unregister(IManifest mod);
}