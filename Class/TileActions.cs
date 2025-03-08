using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;

namespace BuildModeForTilesAndCraftables
{
    public static class TileActions
    {
        public static void PlaceTiles(Rectangle tileArea)
        {
            var currentItem = Game1.player.CurrentItem as StardewValley.Object;
            if (currentItem == null)
            {
                return;
            }

            Dictionary<string, string> floorLookup = Flooring.GetFloorPathItemLookup();
            bool isFloor = (currentItem.Category == -20 || floorLookup.ContainsKey(currentItem.ParentSheetIndex.ToString()));

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

                // If the current item is a fence or gate, use our fence placement logic.
                if (isFence || isGate)
                {
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

                            // Only attempt placement if the tile is placeable.
                            if (!Game1.currentLocation.isTilePlaceable(tileCoord, false))
                                continue;

                            // Create a new fence using the correct constructor:
                            // Fence(tileLocation, itemId, isGate)
                            Fence fence = new Fence(tileCoord, currentItem.ParentSheetIndex.ToString(), isGate);

                            // Trigger the fence's placement action.
                            bool success = fence.placementAction(Game1.currentLocation, x, y, Game1.player);

                            // If placement succeeded (or if the tile is empty), add the fence to the location's objects.
                            if (success || !Game1.currentLocation.objects.ContainsKey(tileCoord))
                            {
                                Game1.currentLocation.objects[tileCoord] = fence;
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
                    return;
                }

                // Normal placement for non-fence, non-floor big craftable objects.
                int itemWidth2 = 1;
                int itemHeight2 = 1;
                int availableRows2 = tileArea.Height / itemHeight2;
                int availableCols2 = tileArea.Width / itemWidth2;
                int totalPlacementsPossible2 = availableRows2 * availableCols2;
                int availableInInventory2 = currentItem.Stack;
                int placementsToDo2 = Math.Min(totalPlacementsPossible2, availableInInventory2);
                int placedCount2 = 0;
                for (int row = 0; row < availableRows2; row++)
                {
                    for (int col = 0; col < availableCols2; col++)
                    {
                        if (placedCount2 >= placementsToDo2)
                            break;
                        int x = tileArea.X + col * itemWidth2;
                        int y = tileArea.Y + row * itemHeight2;
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
                            placedCount2++;
                        }
                    }
                    if (placedCount2 >= placementsToDo2)
                        break;
                }
                if (placedCount2 > 0)
                {
                    currentItem.Stack -= placedCount2;
                    if (currentItem.Stack <= 0)
                        Game1.player.removeItemFromInventory(currentItem);
                }
            }
        }

       
        public static void RemoveTiles(Rectangle tileArea)
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

        
       
        public static void RemoveFloorTiles(Rectangle tileArea)
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

       
        
        public static void RemoveAllButFloorAndBigCraftables(Rectangle tileArea)
        {
            // Your original removeAllbutFloorAndBigCraftables implementation.
        }

        public static void PlaceFencesInSelection(Rectangle selection)
        {
            for (int x = selection.X; x < selection.X + selection.Width; x++)
            {
                for (int y = selection.Y; y < selection.Y + selection.Height; y++)
                {
                    // Create a new fence using the proper constructor.
                    // Parameters: tileLocation, itemId, isGate.
                    // Replace "0" with the desired fence ID if necessary.
                    Fence fence = new Fence(new Vector2(x, y), "0", false);

                    // Trigger the placement action.
                    // The parameters are: the current location, x tile, y tile, and a valid Farmer reference.
                    bool placed = fence.placementAction(Game1.currentLocation, x, y, Game1.player);

                    if (!placed)
                    {
                        //Monitor.Log($"Failed to place fence at tile ({x}, {y})", LogLevel.Warn);
                    }
                }
            }
        }
    }
}
