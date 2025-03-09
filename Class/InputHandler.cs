using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using static BuildModeForTilesAndCraftables.ModEntry;

namespace BuildModeForTilesAndCraftables
{
    public static class InputHandler
    {

        public static void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            var mod = Instance;
            if (!Context.IsWorldReady)
                return;

            // Toggle placement/removal mode using the configured key.
            if (e.Button == mod.Config.ToggleBetweenAddandRemoveTiles.ToSButton())
            {

                CycleBuildMode();
            }

            if (mod.CurrentMode == BuildMode.View)
            {
                return;
            }


            // Use our toolbar bounds.
            Rectangle toolbarBounds = RenderHandler.GetToolbarBounds();
            Point mousePoint = new Point(Game1.getMouseX(false), Game1.getMouseY(false));

            // If the click occurs inside the toolbar area, do not process build mode input.
            if (toolbarBounds.Contains(mousePoint))
                return;


            if (Game1.activeClickableMenu is StardewValley.Menus.CarpenterMenu carpenterMenu)
            {
                // Check if the menu is in the building construction tab.
                // The exact index might vary depending on the game version, but often 0 indicates building construction.
                if (carpenterMenu.IsActive())
                {
                    if (e.Button == mod.Config.TurnOnBuildMode.ToSButton())
                    {
                        Game1.addHUDMessage(new HUDMessage("Cannot open Build Mode when Carpentor Menu is Open", 3));
                    }
                    // Robin's Build Mode is active.
                    return;
                }
            }




            // Toggle custom build mode.
            if (e.Button == mod.Config.TurnOnBuildMode.ToSButton())
            {
                mod.isBuildModeActive = !mod.isBuildModeActive;
                if (mod.isBuildModeActive)
                {
                    Game1.player.canMove = false;  // Freeze the farmer.
                    mod.originalViewport = new Point(Game1.viewport.X, Game1.viewport.Y);
                    mod.buildCameraOffset = Vector2.Zero;

                    if (!mod.musicChanged)
                    {
                        mod.currentMusic = Game1.getMusicTrackName();
                        Game1.changeMusicTrack(Instance.Config.PlayThisMusicInBuildMode);

                    }
                }
                else
                {
                    Game1.changeMusicTrack(mod.currentMusic);
                    mod.isDragging = false;
                    Game1.player.canMove = true;   // Unfreeze the farmer.
                }
                return;
            }

            if (mod.isBuildModeActive)
            {


                // Tab: change selection to the next slot (could be the same as MouseWheelDown).
                if (e.Button == SButton.MouseLeft || e.Button == SButton.C)
                {
                    mod.isUsingTool = true;


                }

            }

            if (!mod.isBuildModeActive)
            {

                return;
            }



            // Process left-click (outside the toolbar area).
            if (e.Button == SButton.MouseLeft)
            {
                if (!mod.isDragging)
                {
                    mod.isDragging = true;
                    // Lock in the viewport when dragging begins.
                    mod.dragViewport = new Point(Game1.viewport.X, Game1.viewport.Y);
                    mod.dragStart = TileUtilities.SnapToTileWorld(mousePoint, mod.dragViewport);
                    mod.dragEnd = mod.dragStart;
                }
                else
                {
                    // Use the locked viewport while ending the drag.
                    mod.dragEnd = TileUtilities.SnapToTileWorld(mousePoint, mod.dragViewport);
                    //dragEnd = SnapToTileWorld(new Point(Game1.getMouseX(false), Game1.getMouseY(false)));
                    Rectangle selection = TileUtilities.GetTileSelectionRectangle(mod.dragStart, mod.dragEnd);
                    if (mod.CurrentMode == BuildMode.Removal)
                    {

                        if (mod.Config.SelectionRemovesBigCraftables)
                        {

                            TileActions.RemoveTiles(selection);
                        }
                        if (mod.Config.SelectionRemovesFloorTiles)
                        {
                            Game1.playSound("pickUpItem");
                            TileActions.RemoveFloorTiles(selection);
                        }


                        /*
                        if (Config.selectionRemoveNonFloorNonBigCraftableItems)
                        {
                            removeAllbutFloorAndBigCraftables(selection);
                        }
                        */



                    }





                    else
                    {
                        Game1.playSound("axe");
                        TileActions.PlaceTiles(selection);
                       


                    }

                    mod.isDragging = false;
                    mod.dragViewport = null; // Clear the locked viewport once done.
                }
            }

            // Right-click cancels the drag selection.
            if (e.Button == SButton.MouseRight)
            {
                if (mod.isDragging)
                {
                    mod.isDragging = false;
                }
            }
        }

        public static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {

            var mod = Instance;
            // Update drag selection if dragging.
            if (mod.isBuildModeActive && mod.isDragging && mod.dragViewport.HasValue)
            {
                mod.dragEnd = TileUtilities.SnapToTileWorld(new Point(Game1.getMouseX(false), Game1.getMouseY(false)), mod.dragViewport);
            }

            // While in build mode, update the buildCameraOffset based on WASD input.
            if (mod.isBuildModeActive)
            {


                //if player uses a took during build mode this will ensure player locked so farmer doesn't move
                if (mod.isUsingTool && !Game1.player.UsingTool)
                {
                    mod.isUsingTool = false;
                    Game1.player.canMove = false;
                }

                MouseState currentMouseState = Mouse.GetState();
                int currentScroll = currentMouseState.ScrollWheelValue;
                int delta = currentScroll - mod.previousScrollValue;

                // If the scroll wheel has moved.
                if (delta > 0)
                {
                    // Scrolled up: move hotbar selection left (wrap-around).
                    Game1.player.CurrentToolIndex = (Game1.player.CurrentToolIndex + 11) % 12;
                }
                else if (delta < 0)
                {
                    // Scrolled down: move hotbar selection right.
                    Game1.player.CurrentToolIndex = (Game1.player.CurrentToolIndex + 1) % 12;
                }

                // Update the previous scroll value for the next tick.
                mod.previousScrollValue = currentScroll;

                KeyboardState keyboardState = Keyboard.GetState();
                int scrollSpeed = 16; // Adjust scroll speed as needed.

                if (mod.isBuildModeActive && !mod.isDragging)
                {
                    if (keyboardState.IsKeyDown(Keys.W))
                        mod.buildCameraOffset.Y -= scrollSpeed;
                    if (keyboardState.IsKeyDown(Keys.S))
                        mod.buildCameraOffset.Y += scrollSpeed;
                    if (keyboardState.IsKeyDown(Keys.A))
                        mod.buildCameraOffset.X -= scrollSpeed;
                    if (keyboardState.IsKeyDown(Keys.D))
                        mod.buildCameraOffset.X += scrollSpeed;
                }


                // Calculate map dimensions in pixels.
                int mapWidth = Game1.currentLocation.map.Layers[0].LayerWidth * TileSize;
                int mapHeight = Game1.currentLocation.map.Layers[0].LayerHeight * TileSize;

                // Calculate the maximum viewport positions.
                int maxViewportX = mapWidth - Game1.viewport.Width;
                int maxViewportY = mapHeight - Game1.viewport.Height;

                // Compute the new viewport position based on the original viewport and camera offset.
                int newViewportX = mod.originalViewport.X + (int)mod.buildCameraOffset.X;
                int newViewportY = mod.originalViewport.Y + (int)mod.buildCameraOffset.Y;

                // Clamp the viewport coordinates so the camera doesn't scroll beyond the map.
                newViewportX = Math.Max(0, Math.Min(newViewportX, maxViewportX));
                newViewportY = Math.Max(0, Math.Min(newViewportY, maxViewportY));

                // Apply the clamped viewport positions.
                Game1.viewport.X = newViewportX;
                Game1.viewport.Y = newViewportY;

                // Reset the buildCameraOffset so it reflects the actual viewport position.
                mod.buildCameraOffset = new Vector2(newViewportX - mod.originalViewport.X, newViewportY - mod.originalViewport.Y);
            }
        }
        // Example of a helper method to cycle build modes.
        public static void CycleBuildMode()
        {
            var mod = Instance;
            if (mod.Config.EnableViewMode)
            {
                // Cycle through all modes: Placement, Removal, View.
                int modeCount = Enum.GetValues(typeof(BuildMode)).Length;
                mod.CurrentMode = (BuildMode)(((int)mod.CurrentMode + 1) % modeCount);
            }
            else
            {
                // Only toggle between Placement and Removal
                mod.CurrentMode = mod.CurrentMode == BuildMode.Placement ? BuildMode.Removal : BuildMode.Placement;
            }
        }
    }
}
