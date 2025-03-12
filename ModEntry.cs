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

            gmcm.AddParagraph(
                ModManifest,
                text: () => "Update Hotkeys"
            );
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

            gmcm.AddParagraph(
                ModManifest,
                text: () => "Set Checkerbox placement with your selected area by setting how much columns and rows should be skipped below applies to both placement and removal selections."
            );
                
            gmcm.AddNumberOption(
                mod: ModManifest,
                getValue: () => Instance.Config.Columns,
                setValue: value => Instance.Config.Columns = (int)value,
                name: () => "Column",
                tooltip: () => "",
                min: 0,
                max: 16,
                interval: 1

            );





            gmcm.AddNumberOption(
                mod: ModManifest,
                getValue: () => Instance.Config.Rows,
                setValue: value => Instance.Config.Rows = (int)value,
                name: () => "Row",
                tooltip: () => "",
                min: 0,
                max: 16,
                interval: 1

            );


            gmcm.AddParagraph(
                 ModManifest,
                 text: () => "Items Below Should Removed when Selected? "
             );

            gmcm.AddBoolOption(
                ModManifest,
                name: () => "Remove Floor Tiles?",
                tooltip: () => "Remove mode now removes Floor Tiles like wood planks",
                getValue: () => Config.FloorTiles,
                setValue: value => Config.FloorTiles = value
            );

            gmcm.AddBoolOption(
                ModManifest,
                name: () => "Remove Big Craftables?",
                tooltip: () => "Remove Mode will allow removal of Craftables like Kegs.",
                getValue: () => Config.BigCraftables,
                setValue: value => Config.BigCraftables = value
            );


            gmcm.AddParagraph(
    mod: ModManifest,
    text: () => "Choose a music that plays during build mode"
);


            gmcm.AddTextOption(
                mod: ModManifest,
                getValue: () => Config.PlayThisMusicInBuildMode,
                setValue: value => Config.PlayThisMusicInBuildMode = value,
                name: () => "Music",
                allowedValues: new string[]
                {
                    "50s",
                    "AbigailFlute",
                    "AbigailFluteDuet",
                    "aerobics",
                    "archaeo",
                    "bigDrums",
                    "breezy",
                    "caldera",
                    "Cavern",
                    "christmasTheme",
                    "Cloth",
                    "CloudCountry",
                    "clubloop",
                    "cowboy_boss",
                    "cowboy_outlawsong",
                    "Cowboy_OVERWORLD",
                    "Cowboy_singing",
                    "Cowboy_undead",
                    "crane_game",
                    "crane_game_fast",
                    "Crystal Bells",
                    "Cyclops",
                    "desolate",
                    "distantBanjo",
                    "EarthMine", // repeated thrice in your list, but kept if you need them
                    "EarthMine",
                    "EarthMine",
                    "echos",
                    "elliottPiano",
                    "EmilyDance",
                    "EmilyDream",
                    "EmilyTheme",
                    "end_credits",
                    "event1",
                    "event2",
                    "fall1",
                    "fall2",
                    "fall3",
                    "fallFest",
                    "fieldofficeTentMusic",
                    "FlowerDance",
                    "FrogCave",
                    "FrostMine", // repeated thrice
                    "FrostMine",
                    "FrostMine",
                    "Ghost Synth",
                    "grandpas_theme",
                    "gusviolin",
                    "harveys_theme_jazz",
                    "heavy",
                    "honkytonky",
                    "Icicles",
                    "IslandMusic",
                    "jaunty",
                    "junimoKart",
                    "junimoKart_ghostMusic",
                    "junimoKart_mushroomMusic",
                    "junimoKart_slimeMusic",
                    "junimoKart_whaleMusic",
                    "junimoStarSong",
                    "kindadumbautumn",
                    "LavaMine", // repeated four times
                    "LavaMine",
                    "LavaMine",
                    "LavaMine",
                    "libraryTheme",
                    "MainTheme",
                    "Majestic",
                    "MarlonsTheme",
                    "marnieShop",
                    "mermaidSong",
                    "moonlightJellies",
                    "movie_classic",
                    "movie_nature",
                    "movie_wumbus",
                    "movieTheater",
                    "movieTheaterAfter",
                    "musicboxsong",
                    "Near The Planet Core",
                    "New Snow",
                    "night_market",
                    "Of Dwarves",
                    "Orange",
                    "Overcast",
                    "Pink Petals",
                    "PIRATE_THEME",
                    "PIRATE_THEME(muffled)",
                    "playful",
                    "Plums",
                    "poppy",
                    "raccoonSong",
                    "ragtime",
                    "sad_kid",
                    "sadpiano",
                    "Saloon1",
                    "sam_acoustic1",
                    "sam_acoustic2",
                    "sampractice",
                    "sappypiano",
                    "Secret Gnomes",
                    "SettlingIn",
                    "shaneTheme",
                    "shimmeringbastion",
                    "spaceMusic",
                    "spirits_eve",
                    "spring1",
                    "spring2",
                    "spring3",
                    "springsongs", // repeated thrice
                    "springsongs",
                    "springsongs",
                    "springtown",
                    "Stadium_ambient",
                    "starshoot",
                    "submarine_song",
                    "summer1",
                    "summer2",
                    "summer3",
                    "SunRoom",
                    "sweet",
                    "tickTock",
                    "tinymusicbox",
                    "title_night",
                    "tribal",
                    "Tropical Jam",
                    "VolcanoMines", // repeated twice
                    "VolcanoMines",
                    "VolcanoMines1",
                    "VolcanoMines2",
                    "wavy",
                    "wedding",
                    "winter1",
                    "winter2",
                    "winter3",
                    "WizardSong",
                    "woodsTheme",
                    "XOR"
                },
                formatAllowedValue: null,
                fieldId: null
            );



        }
    }
}
