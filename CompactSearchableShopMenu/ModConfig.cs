using System.Numerics;
using StardewModdingAPI;

namespace CompactSearchableShopMenu;

internal sealed class ModConfig
{
    /// <summary>Number of items per row when a price must be displayed (e.g. regular shops).</summary>
    public int ShopItemPerRow { get; set; } = 4;

    /// <summary>Number of items per row when there are no prices (e.g. dressers, catalogues).</summary>
    public int DresserItemPerRow { get; set; } = 9;

    /// <summary>Number of items to buy when using Shift.</summary>
    public int StackCount_5 { get; set; } = 5;

    /// <summary>Number of items to buy when using Shift+Ctrl.</summary>
    public int StackCount_25 { get; set; } = 25;

    /// <summary>Number of items to buy when using Shift+Ctrl+1.</summary>
    public int StackCount_999 { get; set; } = 999;

    /// <summary>Enable search box and filter tabs in general.</summary>
    public bool EnableSearchAndFilters { get; set; } = true;

    /// <summary>Enable search box.</summary>
    public bool EnableSearch { get; set; } = true;

    /// <summary>Enable search box.</summary>
    public Vector2 SearchBoxOffset { get; set; } = Vector2.Zero;

    /// <summary>Enable filter tabs for categories.</summary>
    public bool EnableTab_Category { get; set; } = true;

    /// <summary>Enable filter tabs that separate seeds into crop, fruit tree seed, and custom bush.</summary>
    public bool EnableTab_DetailedSeeds { get; set; } = true;

    /// <summary>Enable filter tabs for recipe.</summary>
    public bool EnableTab_Recipes { get; set; } = true;

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        ShopItemPerRow = 4;
        DresserItemPerRow = 9;
        EnableSearchAndFilters = true;
        EnableSearch = true;
        SearchBoxOffset = Vector2.Zero;
        // stack count
        StackCount_5 = 5;
        StackCount_25 = 25;
        StackCount_999 = 999;
        // tabs
        EnableTab_Category = true;
        EnableTab_DetailedSeeds = true;
        EnableTab_Recipes = true;
    }

    /// <summary>Add mod config to GMCM if available</summary>
    /// <param name="helper"></param>
    /// <param name="mod"></param>
    public void Register(IModHelper helper, IManifest mod)
    {
        Integration.IGenericModConfigMenuApi? GMCM = helper.ModRegistry.GetApi<Integration.IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu"
        );
        if (!Patches.Success_Search)
            EnableSearchAndFilters = false;

        if (GMCM == null || !(Patches.Success_Grid || Patches.Success_StackCount || Patches.Success_Search))
        {
            helper.WriteConfig(this);
            return;
        }
        GMCM.Register(
            mod: mod,
            reset: () =>
            {
                Reset();
                helper.WriteConfig(this);
            },
            save: () =>
            {
                helper.WriteConfig(this);
            },
            titleScreenOnly: false
        );
        if (Patches.Success_Grid)
        {
            GMCM.AddNumberOption(
                mod,
                getValue: () => ShopItemPerRow,
                setValue: (value) => ShopItemPerRow = value,
                name: I18n.Config_ShopItemPerRow_Name,
                tooltip: I18n.Config_ShopItemPerRow_Description,
                min: 1,
                max: 9
            );
            GMCM.AddNumberOption(
                mod,
                getValue: () => DresserItemPerRow,
                setValue: (value) => DresserItemPerRow = value,
                name: I18n.Config_DresserItemPerRow_Name,
                tooltip: I18n.Config_DresserItemPerRow_Description,
                min: 1,
                max: 9
            );
        }
        else
        {
            GMCM.AddParagraph(mod, I18n.Config_Failed_Grid);
        }
        if (Patches.Success_StackCount)
        {
            GMCM.AddNumberOption(
                mod,
                getValue: () => StackCount_5,
                setValue: (value) => StackCount_5 = value,
                name: I18n.Config_StackCount_5_Name,
                tooltip: I18n.Config_StackCount_5_Description,
                min: 5
            );
            GMCM.AddNumberOption(
                mod,
                getValue: () => StackCount_25,
                setValue: (value) => StackCount_25 = value,
                name: I18n.Config_StackCount_25_Name,
                tooltip: I18n.Config_StackCount_25_Description,
                min: 5
            );
            GMCM.AddNumberOption(
                mod,
                getValue: () => StackCount_999,
                setValue: (value) => StackCount_999 = value,
                name: I18n.Config_StackCount_999_Name,
                tooltip: I18n.Config_StackCount_999_Description,
                min: 5
            );
        }
        else
        {
            GMCM.AddParagraph(mod, I18n.Config_Failed_StackCount);
        }
        if (Patches.Success_Search)
        {
            GMCM.AddBoolOption(
                mod,
                getValue: () => EnableSearchAndFilters,
                setValue: (value) => EnableSearchAndFilters = value,
                name: I18n.Config_EnableSearchAndFilters_Name,
                tooltip: I18n.Config_EnableSearchAndFilters_Description
            );
            GMCM.AddBoolOption(
                mod,
                getValue: () => EnableSearch,
                setValue: (value) => EnableSearch = value,
                name: I18n.Config_EnableSearch_Name,
                tooltip: I18n.Config_EnableSearch_Description
            );
            GMCM.AddTextOption(
                mod,
                getValue: () => $"{SearchBoxOffset.X} {SearchBoxOffset.Y}",
                setValue: (value) =>
                {
                    string[] pos = value.Split(' ');
                    if (
                        pos.Length == 2
                        && int.TryParse(pos[0].Trim(), out int posX)
                        && int.TryParse(pos[1].Trim(), out int posY)
                    )
                        SearchBoxOffset = new(posX, posY);
                    else
                        SearchBoxOffset = Vector2.Zero;
                },
                name: I18n.Config_SearchBoxOffset_Name,
                tooltip: I18n.Config_SearchBoxOffset_Description
            );
            GMCM.AddBoolOption(
                mod,
                getValue: () => EnableTab_Category,
                setValue: (value) => EnableTab_Category = value,
                name: I18n.Config_EnableTab_Category_Name,
                tooltip: I18n.Config_EnableTab_Category_Description
            );
            GMCM.AddBoolOption(
                mod,
                getValue: () => EnableTab_DetailedSeeds,
                setValue: (value) => EnableTab_DetailedSeeds = value,
                name: I18n.Config_EnableTab_DetailedSeeds_Name,
                tooltip: I18n.Config_EnableTab_DetailedSeeds_Description
            );
            GMCM.AddBoolOption(
                mod,
                getValue: () => EnableTab_Recipes,
                setValue: (value) => EnableTab_Recipes = value,
                name: I18n.Config_EnableTab_Recipes_Name,
                tooltip: I18n.Config_EnableTab_Recipes_Description
            );
        }
        else
        {
            GMCM.AddParagraph(mod, I18n.Config_Failed_Search);
        }
    }
}
