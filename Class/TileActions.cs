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

        //PlaceTiles handles placement using the other placement methods.
        // PlaceTiles handles placement using the other placement methods.
        public static void PlaceTiles(Rectangle tileArea)
        {
            var currentObject = Game1.player.CurrentItem as StardewValley.Object;
            if (currentObject == null)
                return;

            var currentItem = Game1.player.CurrentItem as StardewValley.Item;
            if (currentItem == null)
                return;

            // 1) Check if this item is a tree seed.
            if (IsTreeSeed(currentObject, out int treeType))
            {
                PlaceTreeSeeds(tileArea, currentObject, treeType);
                return; // Stop here so we don't do normal placement.
            }

            // 1.1) Check if this item is a normal seed (category -74 in SDV).
            if (currentObject.Category == -74)
            {
                PlaceSeed(tileArea, currentObject);
                return;
            }

            // 1.2) Check if the item is a fish item (assuming category -4 for fish).
            if (currentObject.type.Value.Contains("Fish"))
            {
              
                PlaceFish(tileArea, currentObject);
                return;
            }

            // 2) Check for Grass Starter items.
            if (currentItem.ParentSheetIndex == 297 || currentItem.ItemId.Equals("BlueGrassStarter"))
            {
                PlaceGrass(tileArea, currentObject, currentItem);
                return;
            }

            // 3) Otherwise, use your existing logic for floor, fences, normal objects, etc.
            Dictionary<string, string> floorLookup = Flooring.GetFloorPathItemLookup();
            bool isFloor = (currentObject.Category == -20 || floorLookup.ContainsKey(currentObject.ParentSheetIndex.ToString()));
            bool isChest = currentObject is StardewValley.Objects.Chest ||
                           (currentObject.Name.ToLower().Contains("chest") && !currentObject.Name.ToLower().Contains("casket"));
            bool isFence = currentObject.Name.ToLower().Contains("fence");
            bool isGate = currentObject.Name.ToLower().Contains("gate");

            if (isFloor)
            {
                PlaceFlooring(tileArea, currentObject, floorLookup);
            }
            else if (isChest)
            {
                // Prevent placement of normal chests.
                return;
            }
            else if (isFence || isGate)
            {
                PlaceFenceAndGate(tileArea, currentObject, isGate);
            }
            else
            {
                PlaceNormalPlacement(tileArea, currentObject);
            }
        }


        private static void PlaceSeed(Rectangle tileArea, StardewValley.Object seedItem)
        {
            int availableRows = tileArea.Height;
            int availableCols = tileArea.Width;
            int placementsAvailable = seedItem.Stack; // Number of seeds available

            for (int row = 0; row < availableRows && placementsAvailable > 0; row++)
            {
                for (int col = 0; col < availableCols && placementsAvailable > 0; col++)
                {
                    int x = tileArea.X + col;
                    int y = tileArea.Y + row;
                    Vector2 tile = new Vector2(x, y);

                    // Check if the tile is generally placeable and doesn't already have an object.
                    if (!Game1.currentLocation.isTilePlaceable(tile, false) ||
                        Game1.currentLocation.objects.ContainsKey(tile))
                        continue;

                    // Check if the tile is diggable.
                    string diggable = Game1.currentLocation.doesTileHaveProperty(x, y, "Diggable", "Back");
                    if (string.IsNullOrEmpty(diggable) || !diggable.Equals("T"))
                        continue;

                    // Check if there is already a terrain feature (like tilled soil).
                    if (Game1.currentLocation.terrainFeatures != null &&
                        Game1.currentLocation.terrainFeatures.ContainsKey(tile))
                    {
                        // If the terrain feature is HoeDirt with an existing crop, skip planting.
                        if (Game1.currentLocation.terrainFeatures[tile] is HoeDirt existingDirt)
                        {
                            if (existingDirt.crop != null)
                                continue; // There's already a planted crop here.
                            else
                            {
                                // No crop: remove it to allow planting.
                                Game1.currentLocation.terrainFeatures.Remove(tile);
                            }
                        }
                        else
                        {
                            // If it's some other terrain feature, skip planting.
                            continue;
                        }
                    }

                    // Create a new HoeDirt instance for planting.
                    HoeDirt newDirt = new HoeDirt();

                    // Add the new HoeDirt to the location.
                    Game1.currentLocation.terrainFeatures.Add(tile, newDirt);

                    // Plant the seed using the seed's ParentSheetIndex.
                    bool planted = newDirt.plant(seedItem.ParentSheetIndex.ToString(), Game1.player, false);

                    // If planting was successful and a crop was created, fully mature it.
                    if (planted && newDirt.crop != null)
                    {
                        newDirt.crop.currentPhase.Value = newDirt.crop.phaseDays.Count - 1;
                        newDirt.crop.dayOfCurrentPhase.Value = 0;
                        newDirt.crop.fullyGrown.Value = true;
                        placementsAvailable--;
                    }
                    else
                    {
                        // If planting failed, remove the added HoeDirt.
                        Game1.currentLocation.terrainFeatures.Remove(tile);
                    }
                }
            }

            // Update the seed inventory count based only on the number of seeds actually planted.
            int seedsUsed = seedItem.Stack - placementsAvailable;
            seedItem.Stack -= seedsUsed;
            if (seedItem.Stack <= 0)
                Game1.player.removeItemFromInventory(seedItem);
        }


        private static void PlaceFish(Rectangle tileArea, StardewValley.Object currentItem)
        {
            // "Fish" items (your trash items) are placed similarly to other items,
            // but we skip the standard placement checks (like isTilePlaceable) and only ensure the tile isn't already occupied.
            int availableRows = tileArea.Height;
            int availableCols = tileArea.Width;
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
                    int x = tileArea.X + col;
                    int y = tileArea.Y + row;
                    Vector2 tileCoord = new Vector2(x, y);

                    // For fish/trash items, we only need to ensure the tile doesn't already hold an object.
                    if (Game1.currentLocation.objects.ContainsKey(tileCoord))
                        continue;

                    StardewValley.Object placedFish = currentItem.getOne() as StardewValley.Object;
                    if (placedFish == null)
                        continue;
                    placedFish.Stack = 1;
                    placedFish.TileLocation = tileCoord;
                    Game1.currentLocation.objects.Add(tileCoord, placedFish);
                    placedCount++;
                }
            }
            // Update inventory.
            currentItem.Stack -= placedCount;
            if (currentItem.Stack <= 0)
                Game1.player.removeItemFromInventory(currentItem);
        }


        private static void PlaceGrass(Rectangle tileArea, StardewValley.Object currentItem, StardewValley.Item currentGrass)
        {
            // Determine grass type: 1 for normal, 7 for blue.
            int grassType = currentGrass.ItemId.Equals("BlueGrassStarter") ? 7 : 1;
            int availableRows = tileArea.Height;
            int availableCols = tileArea.Width;
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
                    int x = tileArea.X + col;
                    int y = tileArea.Y + row;
                    Vector2 tile = new Vector2(x, y);

                    // Only place if the tile is valid.
                    if (Game1.currentLocation.isTilePlaceable(tile, false) &&
                        !Game1.currentLocation.terrainFeatures.ContainsKey(tile) &&
                        !Game1.currentLocation.objects.ContainsKey(tile) &&
                        Game1.currentLocation.isTilePassable(tile))
                    {
                        Grass newGrass = new Grass(grassType, 4);
                        Game1.currentLocation.terrainFeatures.Add(tile, newGrass);
                        placedCount++;
                    }
                }
                if (placedCount >= placementsToDo)
                    break;
            }
            // Update inventory.
            currentItem.Stack -= placedCount;
            if (currentItem.Stack <= 0)
                Game1.player.removeItemFromInventory(currentItem);
        }


        private static void PlaceFlooring(Rectangle tileArea, StardewValley.Object currentItem, Dictionary<string, string> floorLookup)
        {
            int itemWidth = currentItem.bigCraftable.Value ? 2 : 1;
            int itemHeight = currentItem.bigCraftable.Value ? 2 : 1;
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
                    int startX = tileArea.X + col * itemWidth;
                    int startY = tileArea.Y + row * itemHeight;
                    bool canPlaceBlock = true;

                    // Check each tile block for placement conditions.
                    for (int dx = 0; dx < itemWidth; dx++)
                    {
                        for (int dy = 0; dy < itemHeight; dy++)
                        {
                            Vector2 tile = new Vector2(startX + dx, startY + dy);
                            bool objectPresent = Game1.currentLocation.objects.ContainsKey(tile);
                            if ((Game1.currentLocation.terrainFeatures != null && Game1.currentLocation.terrainFeatures.ContainsKey(tile)) ||
                                objectPresent ||
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

                    // Lookup floor ID.
                    string floorId;
                    if (!floorLookup.TryGetValue(currentItem.ParentSheetIndex.ToString(), out floorId))
                    {
                        floorId = currentItem.ParentSheetIndex.ToString();
                    }
                    Flooring newFloor = new Flooring(floorId);
                    Vector2 placementTile = new Vector2(startX, startY);

                    // Only add flooring if the starting tile is completely empty.
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


        private static void PlaceFenceAndGate(Rectangle tileArea, StardewValley.Object currentItem, bool isGate)
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

                    // Only attempt placement if the tile is valid.
                    if (!Game1.currentLocation.isTilePlaceable(tileCoord, false))
                        continue;

                    Fence fence = new Fence(tileCoord, currentItem.ParentSheetIndex.ToString(), isGate);
                    bool success = fence.placementAction(Game1.currentLocation, x, y, Game1.player);

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
        }


        private static void PlaceNormalPlacement(Rectangle tileArea, StardewValley.Object currentItem)
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

                    // Only attempt placement if the tile is valid.
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
                        placedCount++;
                        continue;
                    }

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


        private static readonly Dictionary<string, int> SeedToTreeTypeMap = new Dictionary<string, int>
        {
            // Numeric IDs stored as strings
            ["309"] = 1, // Acorn
            ["310"] = 2, // Maple Seed
            ["311"] = 3, // Pine Cone
            ["891"] = 7, // Mahogany Seed
            ["292"] = 8, // Some modded seed?
            ["88"] = 6, // If you need 9 for some items, you'd need custom logic
                        // String keys for custom seeds
            ["MossySeed"] = 10,
            ["MysticTreeSeed"] = 13
            // etc...
        };


        private static bool IsTreeSeed(StardewValley.Object obj, out int treeType)
        {
            // First, try by numeric ID
            string key = obj.ParentSheetIndex.ToString();
            if (SeedToTreeTypeMap.TryGetValue(key, out treeType))
                return true;

            // Otherwise, try by item name (for seeds like "MossySeed", "MysticTreeSeed", etc.)
            string nameKey = obj.Name;
            if (SeedToTreeTypeMap.TryGetValue(nameKey, out treeType))
                return true;

            treeType = -1;
            return false;
        }


        private static void PlaceTreeSeeds(Rectangle tileArea, StardewValley.Object currentSeed, int treeType)
        {

            int availableRows = tileArea.Height;
            int availableCols = tileArea.Width;
            int totalPlacementsPossible = availableRows * availableCols;
            int availableInInventory = currentSeed.Stack;
            int placementsToDo = Math.Min(totalPlacementsPossible, availableInInventory);
            int placedCount = 0;

            for (int row = 0; row < availableRows; row++)
            {
                for (int col = 0; col < availableCols; col++)
                {
                    if (placedCount >= placementsToDo)
                        break;

                    int x = tileArea.X + col;
                    int y = tileArea.Y + row;
                    Vector2 tile = new Vector2(x, y);

                    if (Game1.currentLocation.isTilePlaceable(tile, false) &&
                        !Game1.currentLocation.objects.ContainsKey(tile) &&
                        !Game1.currentLocation.terrainFeatures.ContainsKey(tile))
                    {

                        Tree newTree = new Tree(treeType.ToString(), 5, false);

                        Game1.currentLocation.terrainFeatures.Add(tile, newTree);
                        placedCount++;
                    }
                }
                if (placedCount >= placementsToDo)
                    break;
            }

            // Remove placed seeds from inventory
            currentSeed.Stack -= placedCount;
            if (currentSeed.Stack <= 0)
                Game1.player.removeItemFromInventory(currentSeed);
        }


        //All removetiles functions are manually called.
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
                        string TreeType = tree.treeType.ToString();
                        string seedItem = null;
                        if (TreeType.Equals("1"))
                        {
                            seedItem = "309";
                        }
                        else if (TreeType.Equals("2"))
                        {
                            seedItem = "310";
                        }
                        else if (TreeType.Equals("3"))
                        {
                            seedItem = "311";
                        }
                        else if (TreeType.Equals("7"))
                        {
                            seedItem = "891";
                        }
                        else if (TreeType.Equals("8"))
                        {
                            seedItem = "292";
                        }
                        else if (TreeType.Equals("6"))
                        {
                            seedItem = "88";
                        }
                        else if (TreeType.Equals("9"))
                        {
                            seedItem = "88";
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


        public static void RemoveGrassFeatures(Rectangle tileArea)
        {

            if (Game1.currentLocation.terrainFeatures == null)
                return;

            List<Vector2> keys = new List<Vector2>(Game1.currentLocation.terrainFeatures.Keys);

            foreach (Vector2 key in keys)
            {

                if (key.X >= tileArea.X && key.X < tileArea.X + tileArea.Width &&
                    key.Y >= tileArea.Y && key.Y < tileArea.Y + tileArea.Height)
                {

                    if (Game1.currentLocation.terrainFeatures[key] is Grass grass)
                    {
                        StardewValley.Object returnItem = null;

                        if (grass.grassType.Get() == 7)
                        {
                            returnItem = new StardewValley.Object("BlueGrassStarter", 1);
                        }
                        else if (grass.grassType.Get() == 1)
                        {
                            returnItem = new StardewValley.Object("297", 1);
                        }

                        // If we have a valid item, add it to the player's inventory.
                        if (returnItem != null)
                        {
                            bool added = Game1.player.addItemToInventoryBool(returnItem);
                            if (added)
                            {
                                // Remove the grass feature once its corresponding item is added.
                                Game1.currentLocation.terrainFeatures.Remove(key);
                            }
                        }
                    }
                }
            }
        }


        //can not remove plants as seeds, and untill the soil, furtilizers are not removed and will be lost
        public static void RemovePlantedSeeds(Rectangle tileArea)
        {
            // If there are no terrain features (like HoeDirt) to check, just exit.
            if (Game1.currentLocation.terrainFeatures == null)
                return;

            // Copy the keys so we can safely modify terrainFeatures while iterating.
            List<Vector2> keys = new List<Vector2>(Game1.currentLocation.terrainFeatures.Keys);

            foreach (Vector2 tile in keys)
            {
                // Check if this tile is in the selected rectangle.
                if (tile.X >= tileArea.X && tile.X < tileArea.X + tileArea.Width &&
                    tile.Y >= tileArea.Y && tile.Y < tileArea.Y + tileArea.Height)
                {
                    // If this terrain feature is HoeDirt, it might have a planted crop.
                    if (Game1.currentLocation.terrainFeatures[tile] is HoeDirt dirt)
                    {
                        // Check if there's a crop planted.
                        if (dirt.crop != null)
                        {
                            // Convert the crop’s netSeedIndex (a NetString) to an integer.
                            if (!int.TryParse(dirt.crop.netSeedIndex.Value, out int seedIndex))
                            {
                                // If conversion fails, skip processing this tile.
                                continue;
                            }

                            // Create the corresponding seed object using the seed index.
                            StardewValley.Object seedItem = new StardewValley.Object(seedIndex.ToString(), 1);
                            bool added = Game1.player.addItemToInventoryBool(seedItem);
                            if (added)
                            {
                                // Attempt to recover fertilizer (or speed growth item) if it exists.
                                if (int.TryParse(dirt.fertilizer.Value, out int fertValue) && fertValue > 0)
                                {
                                    StardewValley.Object fertItem = new StardewValley.Object(fertValue.ToString(), 1);
                                    Game1.player.addItemToInventoryBool(fertItem);
                                }

                                // Remove the HoeDirt (which contains both the crop and the fertilizer)
                                // so that the tile reverts back to untilled ground.
                                Game1.currentLocation.terrainFeatures.Remove(tile);
                            }
                        }
                    }
                }
            }
        }


    }
}
