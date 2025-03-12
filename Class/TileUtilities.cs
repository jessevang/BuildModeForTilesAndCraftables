using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using static BuildModeForTilesAndCraftables.ModEntry;

namespace BuildModeForTilesAndCraftables
{
    public static class TileUtilities
    {
        private const int TileSize = 64;

        public static Point SnapToTileWorld(Point screenPoint, Point? viewportOverride = null)
        {
            int offsetX = viewportOverride?.X ?? Game1.viewport.X;
            int offsetY = viewportOverride?.Y ?? Game1.viewport.Y;

            // screenPoint is expected to be unscaled now
            int worldX = screenPoint.X + offsetX;
            int worldY = screenPoint.Y + offsetY;
            int tileX = worldX / TileSize;
            int tileY = worldY / TileSize;
            return new Point(tileX, tileY);
        }


        public static Rectangle GetTileSelectionRectangle(Point startTile, Point endTile)
        {
            int x = Math.Min(startTile.X, endTile.X);
            int y = Math.Min(startTile.Y, endTile.Y);
            int width = Math.Abs(startTile.X - endTile.X) + 1;
            int height = Math.Abs(startTile.Y - endTile.Y) + 1;
            return new Rectangle(x, y, width, height);
        }

      


        public static void DrawTileOverlay(SpriteBatch spriteBatch, int tileX, int tileY, BuildMode currentMode)
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
            float zoom = Game1.options.zoomLevel;
            float uiScale = Game1.options.uiScale;
            int screenX = (int)(((tileX * TileSize * zoom) - (Game1.viewport.X * zoom)) / uiScale);
            int screenY = (int)(((tileY * TileSize * zoom) - (Game1.viewport.Y * zoom)) / uiScale);
            int drawTileSize = (int)(TileSize * zoom / uiScale);
            Rectangle tileRect = new Rectangle(screenX, screenY, drawTileSize, drawTileSize);
            spriteBatch.Draw(Game1.staminaRect, tileRect, fillColor);
            DrawRectangleOutline(spriteBatch, tileRect, Color.Black * 0.2f, 2);
        }

        public static void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        }
    }
}
