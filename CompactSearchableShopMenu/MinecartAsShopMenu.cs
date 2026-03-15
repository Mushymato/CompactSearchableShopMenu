using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.GameData.Minecarts;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

public sealed class MinecartDestinationEntry(string responseKey, string responseLabel, Action<string>? doMinecart)
    : ISalable,
        IHaveItemTypeId
{
    public string TypeDefinitionId => "(Salable)";

    public string QualifiedItemId => TypeDefinitionId + responseKey;

    public string DisplayName => responseLabel;

    public string Name => responseKey;

    public bool IsRecipe { get; set; } = false;
    public int Stack { get; set; } = 1;
    public int Quality { get; set; } = 0;

    public bool actionWhenPurchased(string shopId)
    {
        Game1.exitActiveMenu();
        doMinecart?.Invoke(responseKey);
        return true;
    }

    public int addToStack(Item stack) => 0;

    public bool appliesProfitMargins() => false;

    public bool CanBuyItem(Farmer farmer) => true;

    public bool canStackWith(ISalable other) => false;

    public void drawInMenu(
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth,
        StackDrawType drawStackNumber,
        Color color,
        bool drawShadow
    ) { }

    public void FixQuality() { }

    public void FixStackSize() { }

    public string getDescription() => responseLabel;

    public string GetItemTypeId() => TypeDefinitionId;

    public ISalable GetSalableInstance() => this;

    public bool IsInfiniteStock() => true;

    public int maximumStackSize() => 1;

    public int salePrice(bool ignoreProfitMargins = false) => 0;

    public int sellToStorePrice(long specificPlayerID = -1) => -1;

    public bool ShouldDrawIcon() => false;
}

internal static class MinecartAsShopMenu
{
    internal const string MinecartShopId = $"{ModEntry.ModId}_Minecart";
    internal const string MinecartShopPortrait = $"{MinecartShopId}/Portrait";

    public static void Show(
        GameLocation location,
        string prompt,
        List<KeyValuePair<string, string>> responses,
        Action<string> on_response,
        bool auto_select_single_choice,
        bool addCancel,
        int itemsPerPage,
        string networkId
    )
    {
        if (!ModEntry.Config.EnableMinecartAsShopMenu)
        {
            location.ShowPagedResponses(
                prompt,
                responses,
                on_response,
                auto_select_single_choice,
                addCancel,
                itemsPerPage
            );
            return;
        }
        List<ISalable> salableDestinations =
        [
            .. responses.Select<KeyValuePair<string, string>, ISalable>(kv => new MinecartDestinationEntry(
                kv.Key,
                kv.Value,
                on_response
            )),
        ];
        if (addCancel)
            salableDestinations.Add(
                new MinecartDestinationEntry(
                    "cancel",
                    Game1.content.LoadString("Strings\\Locations:MineCart_Destination_Cancel"),
                    null
                )
            );

        Texture2D? portraitTexture = null;
        string networkMinecartShopPortrait = string.Concat(MinecartShopPortrait, "/", networkId);
        if (Game1.content.DoesAssetExist<Texture2D>(networkMinecartShopPortrait))
        {
            portraitTexture = Game1.content.Load<Texture2D>(networkMinecartShopPortrait);
        }
        else if (Game1.content.DoesAssetExist<Texture2D>(MinecartShopPortrait))
        {
            portraitTexture = Game1.content.Load<Texture2D>(MinecartShopPortrait);
        }

        Game1.activeClickableMenu = new ShopMenu(MinecartShopId, salableDestinations)
        {
            portraitTexture = portraitTexture,
            potraitPersonDialogue = prompt,
        };
    }
}
