using StardewModdingAPI;

namespace ShopMenuOverhaul;

internal sealed class ModConfig
{
    /// <summary>Number of items per row when a price must be displayed (e.g. regular shops).</summary>
    public int ShopItemPerRow { get; set; } = 3;
    /// <summary>Number of items per row when there are no prices (e.g. dressers, catalogues).</summary>
    public int DresserItemPerRow { get; set; } = 9;

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        ShopItemPerRow = 3;
        DresserItemPerRow = 9;
    }

    /// <summary>Add mod config to GMCM if available</summary>
    /// <param name="helper"></param>
    /// <param name="mod"></param>
    public void Register(IModHelper helper, IManifest mod)
    {
        Integration.IGenericModConfigMenuApi? GMCM = helper.ModRegistry.GetApi<Integration.IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu"
        );
        if (GMCM == null)
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
        GMCM.AddNumberOption(
            mod,
            getValue: () => ShopItemPerRow,
            setValue: (value) => ShopItemPerRow = value,
            name: I18n.Config_ShopItemPerRow_Name,
            tooltip: I18n.Config_DresserItemPerRow_Description,
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
}
