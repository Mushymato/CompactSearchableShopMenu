global using SObject = StardewValley.Object;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;

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

    private const string DefaultTabIconName = "mushymato.CompactSearchableShopMenu/icon/default";
    internal static Texture2D DefaultTabIcon => Game1.content.Load<Texture2D>(DefaultTabIconName);

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
        if (e.NameWithoutLocale.IsEquivalentTo(DefaultTabIconName))
        {
            e.LoadFrom(LoadDefaultTabIcon, AssetLoadPriority.Low);
        }
    }

    private Texture2D LoadDefaultTabIcon()
    {
        Texture2D tx = new(Game1.graphics.GraphicsDevice, 16, 16);
        Color[] array = new Color[tx.GetElementCount()];
        for (int i = 0; i < tx.GetElementCount(); i++)
        {
            array[i] = Color.Transparent;
        }
        Color[] cursorsData = new Color[Game1.mouseCursors.GetElementCount()];
        int defaultIconWidth = tx.ActualWidth;
        int cursorsWidth = Game1.mouseCursors.ActualWidth;
        Game1.mouseCursors.GetData(cursorsData);
        for (int x = 0; x < 7; x++)
        {
            for (int y = 0; y < 13; y++)
            {
                array[(2 + y) * defaultIconWidth + 5 + x] = cursorsData[(357 + y) * cursorsWidth + 330 + x];
            }
        }
        tx.SetData(array);
        return tx;
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
