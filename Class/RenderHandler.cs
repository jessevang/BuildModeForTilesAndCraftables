using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using static BuildModeForTilesAndCraftables.ModEntry;

namespace BuildModeForTilesAndCraftables
{
    public static class RenderHandler
    {
        public static Rectangle GetToolbarBounds()
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

        public static void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            var mod = Instance;
            if (!mod.isBuildModeActive)
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



            string modeText = mod.CurrentMode switch
            {
                BuildMode.Placement => $"Placement Mode ({mod.Config.ToggleBetweenAddandRemoveTiles}: toggle mode)",
                BuildMode.Removal => $"Removal Mode ({mod.Config.ToggleBetweenAddandRemoveTiles}: toggle mode)",
                BuildMode.View => $"View Mode ({mod.Config.ToggleBetweenAddandRemoveTiles}: toggle mode)",
                _ => ""
            };

            SpriteText.drawString(spriteBatch, modeText, dialogBox.X + 10, dialogBox.Y + 10);
            SpriteText.drawString(spriteBatch, $"LMB: Select/Drag | RMB: Cancel | {mod.Config.TurnOnBuildMode}: Exit", dialogBox.X + 20, dialogBox.Y + 60);


            //removes tile on mouse if in view mode
            if (mod.CurrentMode == BuildMode.View)
            {
                return;
            }

            // Only draw the build mode highlight if the mouse is not over the toolbar.
            if (!toolbarBounds.Contains(mousePoint))
            {
                if (!mod.isDragging)
                {
                    Point hoverTile = TileUtilities.SnapToTileWorld(mousePoint);
                    TileUtilities.DrawTileOverlay(spriteBatch, hoverTile.X, hoverTile.Y, mod.CurrentMode);
                }
                else
                {
                    Rectangle selection = TileUtilities.GetTileSelectionRectangle(mod.dragStart, mod.dragEnd);
                    for (int x = selection.X; x < selection.X + selection.Width; x++)
                    {
                        for (int y = selection.Y; y < selection.Y + selection.Height; y++)
                        {
                            TileUtilities.DrawTileOverlay(spriteBatch, x, y, mod.CurrentMode);
                        }
                    }
                }
            }


        }

    }
}
