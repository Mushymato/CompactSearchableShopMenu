global using SObject = StardewValley.Object;
using CompactSearchableShopMenu.Integration;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

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

    internal static bool HasMod_BiggerBackpack = false;

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        Config = Helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        Harmony harmony = new(ModManifest.UniqueID);
        Patches.Patch(helper, harmony);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Config.Register(Helper, ModManifest);
        HasMod_BiggerBackpack = Helper.ModRegistry.IsLoaded("spacechase0.BiggerBackpack");
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
