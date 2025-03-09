using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework.Input;
using GenericModConfigMenu;


namespace BuildModeForTilesAndCraftables
{
    public class ModEntry : Mod
    {
        public const int TileSize = 64;
        public Point originalViewport;
        public Vector2 buildCameraOffset = Vector2.Zero;
        public Point? dragViewport = null;

        private IGenericModConfigMenuApi GenericModConfigMenuAPI;
        public ModConfig Config { get; private set; }
        public static ModEntry Instance { get; private set; }

        // Shared state used across modules.
        public bool isBuildModeActive { get; set; }
        public Point originalViewPoint { get; set; }
        public bool isDragging { get; set; }
        public Point dragStart { get; set; }
        public Point dragEnd { get; set; }
        public int previousScrollValue { get; set; }
        public bool musicChanged { get; set; }
        public bool isUsingTool { get; set; }
        public string currentMusic { get; set; }
        public enum BuildMode
        {
            Placement,
            Removal,
            View
        }
        public BuildMode CurrentMode { get; set; } = BuildMode.Placement;

        // Provide access to the mod helper for other classes.
        public IModHelper ModHelper { get; private set; }





        public override void Entry(IModHelper helper)
        {
            Instance = this;
            ModHelper = helper;
            Config = helper.ReadConfig<ModConfig>() ?? new ModConfig();

            // Subscribe to events. (Note: methods now reside in other files.)
            helper.Events.Input.ButtonPressed += InputHandler.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += InputHandler.OnUpdateTicked;
            helper.Events.Display.RenderedHud += RenderHandler.OnRenderedHud;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Uses Generic Mod Config Menu API to build a config UI.
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            // Register the mod.
            gmcm.Register(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));

            // Add configuration options.
            gmcm.AddKeybind(
                ModManifest,
                name: () => "Build Mode",
                tooltip: () => "Toggles On/Off Build Mode",
                getValue: () => Config.TurnOnBuildMode.ToSButton(),
                setValue: value => Config.TurnOnBuildMode = (Keys)value
            );

            gmcm.AddKeybind(
                ModManifest,
                name: () => "Placement / Removal",
                tooltip: () => "Once in build mode this hotkey toggles between Placement mode and Removal Mode",
                getValue: () => Config.ToggleBetweenAddandRemoveTiles.ToSButton(),
                setValue: value => Config.ToggleBetweenAddandRemoveTiles = (Keys)value
            );

            gmcm.AddBoolOption(
                ModManifest,
                name: () => "Enable View Mode?",
                tooltip: () => "Adds View mode with cycling through Remove or Place. Generally used for other mods to enable mouse click",
                getValue: () => Config.EnableViewMode,
                setValue: value => Config.EnableViewMode = value
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
    }
}
