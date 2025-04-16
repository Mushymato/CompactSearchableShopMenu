using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed class SearchContext : IDisposable
{
    internal const int SEARCH_ID = ShopMenu.region_shopButtonModifier * 2;

    private readonly ShopMenu shopMenu;
    private readonly TextBox searchBox;
    private readonly ClickableComponent searchBoxCC;
    private List<ISalable>? forSaleAll = null;

    internal SearchContext(ShopMenu shopMenu)
    {
        this.shopMenu = shopMenu;
        searchBox = new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor);
        searchBox.OnEnterPressed += OnEnter;
        searchBox.Text = I18n.Placeholder_Search();
        searchBoxCC = new(Rectangle.Empty, "")
        {
            myID = SEARCH_ID,
            leftNeighborID = ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
            downNeighborID = ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
        };

        Reposition();
        Game1.keyboardDispatcher.Subscriber = searchBox;
        searchBox.Selected = false;
    }

    public void Reposition()
    {
        searchBox.X = shopMenu.xPositionOnScreen;
        searchBox.Y = shopMenu.yPositionOnScreen + shopMenu.height - shopMenu.inventory.height - 12 + 17 * 4 + 4;
        searchBox.Width = 240;
        searchBox.Height = 56;
        searchBoxCC.bounds = new(searchBox.X, searchBox.Y, searchBox.Width, searchBox.Height);
    }

    public void Dispose()
    {
        searchBox.Selected = false;
        if (shopMenu.allClickableComponents?.Contains(searchBoxCC) ?? false)
            shopMenu.allClickableComponents.Remove(searchBoxCC);
        if (Game1.keyboardDispatcher.Subscriber == searchBox)
            Game1.keyboardDispatcher.Subscriber = null;
    }

    public void OnEnter(TextBox sender) => DoSearch();

    public void OnKeyPress(Keys key)
    {
        if (searchBox.Selected)
            DoSearch();
    }

    public void DoSearch()
    {
        string searchText = searchBox.Text;
        shopMenu.forSale = forSaleAll!.Where(fs => fs.DisplayName.ContainsIgnoreCase(searchText)).ToList();
        shopMenu.currentItemIndex = 0;
        Patches.setScrollBarToCurrentIndexMethod?.Invoke(shopMenu, []);
    }

    internal void Activate()
    {
        if (!searchBox.Selected)
        {
            forSaleAll = shopMenu.forSale;
            searchBox.Text = "";
            searchBox.SelectMe();
            shopMenu.currentItemIndex = 0;
            Patches.setScrollBarToCurrentIndexMethod?.Invoke(shopMenu, []);
        }
    }

    internal void Deactivate()
    {
        if (searchBox.Selected)
        {
            searchBox.Text = I18n.Placeholder_Search();
            forSaleAll?.RemoveAll(sale => !shopMenu.itemPriceAndStock.ContainsKey(sale));
            shopMenu.forSale = forSaleAll;
            forSaleAll = null;
            searchBox.Selected = false;
            shopMenu.currentItemIndex = 0;
            Patches.setScrollBarToCurrentIndexMethod?.Invoke(shopMenu, []);
        }
    }

    internal void OnLeftClickPrefix(int x, int y)
    {
        if (searchBoxCC.containsPoint(x, y))
        {
            Activate();
        }
    }

    internal void OnLeftClickPostfix(int x, int y)
    {
        if (!searchBoxCC.containsPoint(x, y) && !shopMenu.forSaleButtons.Any(fsb => fsb.bounds.Contains(x, y)))
        {
            Deactivate();
        }
    }

    public void Draw(SpriteBatch b)
    {
        searchBox.Draw(b);
    }
}
