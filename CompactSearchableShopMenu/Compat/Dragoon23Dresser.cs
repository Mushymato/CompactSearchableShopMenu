using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley.Menus;

namespace CompactSearchableShopMenu.Compat;

internal static class Dragoon23Dresser
{
    internal static FieldInfo ShopMenu_isStorageShop = AccessTools.DeclaredField(typeof(ShopMenu), "_isStorageShop");
    internal static List<ValueTuple<string, string, string>> PatchTargets =>
        [
            new(
                "Dragoon23.CustomizeDresser",
                "DresserPatches.DresserEdits",
                "DrawingFavIcon_And_GridDeleteItemName_PostFix"
            ),
            new(
                "Dragoon23.MoreDresserVariety",
                "DresserVarietyPatches.DresserVarietyEdits",
                "DresserVariety_DrawingFavIcon_And_GridDeleteItemName_PostFix"
            ),
        ];

    internal static void Patch(IModHelper help, Harmony harmony)
    {
        foreach ((string modId, string targetType, string targetMethod) in PatchTargets)
        {
            if (help.ModRegistry.Get(modId) is not IModInfo modInfo)
                continue;
            try
            {
                if (modInfo?.GetType().GetProperty("Mod")?.GetValue(modInfo) is IMod mod)
                {
                    var assembly = mod.GetType().Assembly;
                    if (assembly.GetType(targetType) is Type CustomizeDresser_DresserEdits)
                    {
                        harmony.Patch(
                            original: AccessTools.DeclaredMethod(CustomizeDresser_DresserEdits, targetMethod),
                            prefix: new HarmonyMethod(typeof(Dragoon23Dresser), nameof(ShopMenu_draw_PostFix__Prefix))
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Log($"Failed to patch {modId}-{targetType}::{targetMethod}.\n{ex}");
                continue;
            }
        }
    }

    private static bool ShopMenu_draw_PostFix__Prefix(object[] __args)
    {
        try
        {
            if (Patches.PerRowV == 1 || __args.FirstOrDefault(arg => arg is ShopMenu) is not ShopMenu shopMenu)
                return true;
            if (ShopMenu_isStorageShop.GetValue(shopMenu) is bool isStorageShop)
                return isStorageShop;
        }
        catch (Exception ex)
        {
            ModEntry.Log($"Failed to patch Dragoon23.CustomizeDresser.\n{ex}");
        }
        return true;
    }
}
