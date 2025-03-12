using Microsoft.Xna.Framework.Input;

namespace BuildModeForTilesAndCraftables
{
    public class ModConfig
    {
        public Keys TurnOnBuildMode { get; set; } = Keys.F1;
        public string note000 { get; set; } = "View mode cycles between build and replace mode and allows user to click around in build mode without selecting anything. Generally used to view or to leverage other mods";
        public bool EnableViewMode { get; set; } = false;
        public Keys ToggleBetweenAddandRemoveTiles { get; set; } = Keys.Space;
        public int Columns { get; set;  } = 0;
        public int Rows { get; set; } = 0;
        public bool FloorTiles { get; set; } = true;
        public bool BigCraftables { get; set; } = true;
        public string PlayThisMusicInBuildMode { get; set; } = "Cloth";
    }




}
