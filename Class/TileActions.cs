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

            // Allow caskets to be placed, but block other chests.
            bool isChest = currentItem is StardewValley.Objects.Chest ||
                           (currentItem.Name.ToLower().Contains("chest") && !currentItem.Name.ToLower().Contains("casket"));
            bool isFence = currentItem.Name.ToLower().Contains("fence");
            bool isGate = currentItem.Name.ToLower().Contains("gate");

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
                // Assume terrainFeatures is already initialized.
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
                                // Also block if there is an object already on the tile.
                                if (objectPresent)
                                {
                                    canPlaceBlock = false;
                                    break;
                                }
                                // Additionally, if the tile is not placeable, block it.
                                if (!Game1.currentLocation.isTilePlaceable(tile, false))
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
                        // Only add flooring if the tile is completely empty.
                        if (!Game1.currentLocation.terrainFeatures.ContainsKey(placementTile) &&
                            !Game1.currentLocation.objects.ContainsKey(placementTile))
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
            else if (isChest)
            {
                // Prevent placement of normal chests.
                return;
            }
            else if (isFence || isGate)
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
            else
            {
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

                        // Only attempt placement if the tile is placeable and empty.
                        if (!Game1.currentLocation.isTilePlaceable(tileCoord, false) ||
                            Game1.currentLocation.objects.ContainsKey(tileCoord))
                            continue;

                        // Special handling for caskets.
                        if (currentItem.Name.ToLower().Contains("casket"))
                        {
                            StardewValley.Object placedItem = currentItem.getOne() as StardewValley.Object;
                            if (placedItem == null)
                                continue;
                            placedItem.Stack = 1;
                            placedItem.TileLocation = tileCoord;
                            Game1.currentLocation.objects.Add(tileCoord, placedItem);
                            placedCount2++;
                            continue;
                        }

                        // For other objects, try normal placement.
                        StardewValley.Object normalPlacedItem = currentItem.getOne() as StardewValley.Object;
                        if (normalPlacedItem == null)
                            continue;
                        normalPlacedItem.Stack = 1;
                        bool success = false;
                        try
                        {
                            success = normalPlacedItem.placementAction(Game1.currentLocation, (int)tileCoord.X, (int)tileCoord.Y, Game1.player);
                        }
                        catch (ArgumentException)
                        {
                            success = false;
                        }
                        if (success)
                        {
                            if (!Game1.currentLocation.objects.ContainsKey(tileCoord))
                                Game1.currentLocation.objects.Add(tileCoord, normalPlacedItem);
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
                        // Prevent removal of chests.
                        if (obj is StardewValley.Objects.Chest ||
                            (!string.IsNullOrEmpty(obj.Name) && obj.Name.ToLower().Contains("chest")))
                        {
                            continue;
                        }
                        // Allow removal of all other objects (sprinklers, wood, sap, etc.)
                        bool added = Game1.player.addItemToInventoryBool(obj);
                        if (added)
                        {
                            Game1.currentLocation.objects.Remove(tile);
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


        public static void RemoveTreesAndAddTreeSeeds(Rectangle tileArea)
        {
            // Check that the terrain features have been initialized.
            if (Game1.currentLocation.terrainFeatures == null)
                return;

            // Make a list of all keys so we can modify the dictionary safely while iterating.
            List<Vector2> keys = new List<Vector2>(Game1.currentLocation.terrainFeatures.Keys);
            foreach (Vector2 key in keys)
            {
                // Check if the key (tile) is within the selected area.
                if (key.X >= tileArea.X && key.X < tileArea.X + tileArea.Width &&
                    key.Y >= tileArea.Y && key.Y < tileArea.Y + tileArea.Height)
                {
                    // Check if the terrain feature at this tile is a tree.
                    if (Game1.currentLocation.terrainFeatures[key] is StardewValley.TerrainFeatures.Tree tree)
                    {

                        string TreeType = tree.treeType.ToString();//tree.TextureName;
                        string seedItem = null;
                        if (TreeType.Equals("1"))
                        {
                            seedItem = "(O)309";
                        }
                        else if (TreeType.Equals("2"))
                        {
                            seedItem = "(O)310";
                        }
                        else if (TreeType.Equals("3"))
                        {
                            seedItem = "(O)311";
                        }
                        else if (TreeType.Equals("7"))
                        {
                            seedItem = "(O)891";
                        }
                        else if (TreeType.Equals("8"))
                        {
                            seedItem = "(O)292";
                        }
                        else if (TreeType.Equals("6"))
                        {
                            seedItem = "(O)88";
                        }
                        else if (TreeType.Equals("9"))
                        {
                            seedItem = "(O)88";
                        }
                        else if (TreeType.Equals("10"))
                        {
                            seedItem = "MossySeed";
                        }
                        else if (TreeType.Equals("11"))
                        {
                            seedItem = "MossySeed";
                        }
                        else if (TreeType.Equals("12"))
                        {
                            seedItem = "MossySeed";
                        }
                        else if (TreeType.Equals("13"))
                        {
                            seedItem = "MysticTreeSeed";
                        }


                        if (seedItem != null)
                        {
                            StardewValley.Object treeSeedItem = new StardewValley.Object(seedItem, 1);
                            // Attempt to add the tree seed to the player's inventory.
                            bool added = Game1.player.addItemToInventoryBool(treeSeedItem);
                            if (added)
                            {
                                // If successfully added, remove the tree from the terrain features.
                                Game1.currentLocation.terrainFeatures.Remove(key);
                            }

                        }


                    }
                }
            }
        }




    }

}
