global using SObject = StardewValley.Object;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Shops;

namespace CompactSearchableShopMenu;

public class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif
    private static IMonitor? mon;
    internal static ModConfig Config = null!;
    internal static string ModId = null!;

    internal static bool HasMod_BiggerBackpack = false;

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        ModId = ModManifest.UniqueID;
        Config = Helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Content.AssetRequested += OnAssetRequested;
        Harmony harmony = new(ModManifest.UniqueID);
        Patches.Patch(helper, harmony);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Config.Register(Helper, ModManifest);
        HasMod_BiggerBackpack = Helper.ModRegistry.IsLoaded("spacechase0.BiggerBackpack");
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.StartsWith($"{ModId}/tab/"))
        {
            string tabAssetPath = Path.Combine(
                "assets",
                "tab",
                string.Concat(Path.GetFileName(e.NameWithoutLocale.BaseName), ".png")
            );
            if (File.Exists(tabAssetPath))
                e.LoadFromModFile<Texture2D>(tabAssetPath, AssetLoadPriority.Low);
        }
    }

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }
}
