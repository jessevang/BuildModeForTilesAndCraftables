using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;

namespace BuildModeForTilesAndCraftables
{
    public class ModConfig
    {
        public SButton TurnOnBuildMode { get; set; } = SButton.F1;
        public string note000 { get; set; } = "View mode cycles between build and replace mode and allows user to click around in build mode without selecting anything. Generally used to view or to leverage other mods";
        public bool EnableViewMode { get; set; } = false;
        public SButton ToggleBetweenAddandRemoveTiles { get; set; } = SButton.Space;
        public int Columns { get; set;  } = 0;
        public int Rows { get; set; } = 0;
        public bool FloorTiles { get; set; } = true;
        public bool BigCraftables { get; set; } = true;
        public string PlayThisMusicInBuildMode { get; set; } = "Cloth";
        public SButton TurnOnBuildModeButton { get; set; } = SButton.LeftStick;
        public SButton SelectAndConfirmArea { get; set; } = SButton.ControllerA;
        public SButton CancelSelection { get; set; } = SButton.ControllerB;
        public SButton ToggleBetweenAddandRemoveTilesButton { get; set; } = SButton.RightTrigger;
        public SButton CameraUpButton { get; set; } = SButton.LeftThumbstickUp;
        public SButton CameraLeftButton { get; set; } = SButton.LeftThumbstickLeft;
        public SButton CameraRightButton { get; set; } = SButton.LeftThumbstickRight;
        public SButton CameraDownButton { get; set; } = SButton.LeftThumbstickDown;
    }




}
