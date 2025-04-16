using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed class SearchContext : IDisposable
{
    // TODO: somehow make snap work
    internal const int SEARCH_ID = ShopMenu.region_shopButtonModifier * 2;
    internal const int NO_CATEGORY = -9999;
    internal static readonly Rectangle tabSourceRect = new(16, 368, 16, 16);
    internal const int TAB_OFFSET = 8;

    private readonly WeakReference<ShopMenu> shopMenu;
    private ShopMenu? Shop
    {
        get
        {
            if (shopMenu.TryGetTarget(out ShopMenu? sm))
                return sm;
            return null;
        }
    }
    private readonly TextBox searchBox;
    private readonly ClickableComponent searchBoxCC;
    private readonly Dictionary<int, ClickableTextureComponent> categoryTabs = [];
    private readonly List<int> categoryTabsOrder = [];
    private int categoryCurrent = NO_CATEGORY;

    private List<ISalable>? forSaleAll = null;

    internal SearchContext(ShopMenu shopMenu)
    {
        this.shopMenu = new(shopMenu);
        searchBox = new(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor);
        searchBox.OnEnterPressed += OnEnter;
        searchBox.Text = I18n.Placeholder_Search();
        searchBoxCC = new(Rectangle.Empty, "SEARCH") { myID = SEARCH_ID };

        if (!shopMenu.tabButtons.Any())
        {
            foreach (var sale in Shop!.forSale)
            {
                if (sale is Item item && !categoryTabs.ContainsKey(item.Category) && !item.IsRecipe)
                {
                    string categoryName = StardewValley.Object.GetCategoryDisplayName(item.Category);
                    categoryTabs[item.Category] = new(
                        item.Category.ToString(),
                        Rectangle.Empty,
                        "",
                        categoryName,
                        Game1.mouseCursors,
                        tabSourceRect,
                        4f,
                        false
                    )
                    {
                        item = item,
                    };
                }
            }
            if (categoryTabs.Count > 1)
            {
                categoryTabsOrder.Add(NO_CATEGORY);
                categoryTabsOrder.AddRange(categoryTabs.Keys);
                categoryTabs[NO_CATEGORY] = new(
                    NO_CATEGORY.ToString(),
                    Rectangle.Empty,
                    "",
                    "NO_CATEGORY",
                    Game1.mouseCursors,
                    tabSourceRect,
                    4f,
                    false
                );
            }
            else
            {
                categoryTabs.Clear();
            }
        }

        Reposition();
        Game1.keyboardDispatcher.Subscriber = searchBox;
        searchBox.Selected = false;
    }

    public void Reposition()
    {
        if (Shop == null)
            return;

        searchBox.X = Shop.xPositionOnScreen;
        searchBox.Y = Shop.yPositionOnScreen + Shop.height - Shop.inventory.height - 12 + 17 * 4 + 4;
        searchBox.Width = 240;
        searchBox.Height = 56;
        searchBoxCC.bounds = new(searchBox.X, searchBox.Y, searchBox.Width, searchBox.Height);

        int n = 0;
        foreach (int category in categoryTabsOrder)
        {
            ClickableTextureComponent cct = categoryTabs[category];
            cct.bounds.X = Shop.xPositionOnScreen + n * tabSourceRect.Width * 4 + 20;
            cct.bounds.Y = Shop.yPositionOnScreen - tabSourceRect.Height * 4 + 8;
            if (category == categoryCurrent)
            {
                cct.bounds.Y += TAB_OFFSET;
            }
            cct.bounds.Width = tabSourceRect.Width * 4;
            cct.bounds.Height = tabSourceRect.Height * 4;
            n++;
        }

        categoryCurrent = NO_CATEGORY;
    }

    public void Dispose()
    {
        forSaleAll = null;
        categoryTabsOrder.Clear();
        categoryTabs.Clear();
        searchBox.Selected = false;
        if (Game1.keyboardDispatcher.Subscriber == searchBox)
            Game1.keyboardDispatcher.Subscriber = null;
    }

    public void OnEnter(TextBox sender) => DoSearch();

    public void OnKeyPress(Keys key)
    {
        if (searchBox.Selected)
            DoSearch();
    }

    public bool OnGamePadButton(Buttons button)
    {
        if (
            Shop == null
            || categoryTabsOrder.Count == 0
            || (button != Buttons.RightShoulder && button != Buttons.LeftShoulder)
        )
        {
            return true;
        }
        if (categoryTabs.TryGetValue(categoryCurrent, out ClickableTextureComponent? cctCurr))
        {
            cctCurr.bounds.Y -= TAB_OFFSET;
        }
        int nextIdx = categoryTabsOrder.IndexOf(categoryCurrent);
        if (button == Buttons.RightShoulder)
        {
            nextIdx++;
            if (nextIdx == categoryTabsOrder.Count)
                nextIdx = 0;
        }
        else if (button == Buttons.LeftShoulder)
        {
            nextIdx--;
            if (nextIdx == -1)
                nextIdx = categoryTabsOrder.Count - 1;
        }
        categoryCurrent = categoryTabsOrder[nextIdx];
        if (categoryTabs.TryGetValue(categoryCurrent, out cctCurr))
        {
            cctCurr.bounds.Y += TAB_OFFSET;
        }
        DoSearch();
        return false;
    }

    public void DoSearch()
    {
        if (Shop == null)
            return;

        forSaleAll ??= Shop.forSale;
        IEnumerable<ISalable> forSale = forSaleAll;
        if (categoryCurrent != NO_CATEGORY)
        {
            forSale = forSale.Where(fs => fs is Item item && item.Category == categoryCurrent);
        }
        if (searchBox.Selected)
        {
            string searchText = searchBox.Text;
            forSale = forSale.Where(fs => fs.DisplayName.ContainsIgnoreCase(searchText));
        }
        Shop.forSale = forSale.ToList();
        Shop.currentItemIndex = 0;
        Patches.setScrollBarToCurrentIndexMethod?.Invoke(Shop, []);
    }

    internal void SearchActivate()
    {
        if (Shop == null)
            return;

        if (!searchBox.Selected)
        {
            searchBox.Text = "";
            searchBox.SelectMe();
        }
    }

    internal void SearchDeactivate()
    {
        if (Shop == null)
            return;

        if (searchBox.Selected)
        {
            searchBox.Text = I18n.Placeholder_Search();
            searchBox.Selected = false;
        }
        if (forSaleAll != null)
        {
            forSaleAll.RemoveAll(frs => !Shop.itemPriceAndStock.ContainsKey(frs));
            if (categoryCurrent == NO_CATEGORY)
            {
                Shop.forSale = forSaleAll;
                forSaleAll = null;
                Shop.currentItemIndex = 0;
                Patches.setScrollBarToCurrentIndexMethod?.Invoke(Shop, []);
            }
            else
            {
                DoSearch();
            }
        }
    }

    internal void OnLeftClickPrefix(int x, int y)
    {
        if (Shop == null)
            return;

        if (searchBoxCC.containsPoint(x, y))
        {
            SearchActivate();
        }
        else
        {
            int? clickedCategory = null;
            foreach ((int category, ClickableTextureComponent cct) in categoryTabs)
            {
                if (cct.containsPoint(x, y))
                {
                    cct.bounds.Y += TAB_OFFSET;
                    clickedCategory = category;
                    break;
                }
            }
            if (clickedCategory != null)
            {
                if (categoryTabs.TryGetValue(categoryCurrent, out ClickableTextureComponent? cctCurr))
                {
                    cctCurr.bounds.Y -= TAB_OFFSET;
                }
                int prevCategory = categoryCurrent;
                categoryCurrent = (int)clickedCategory;
                if (categoryCurrent != prevCategory)
                {
                    DoSearch();
                }
            }
        }
    }

    internal void OnLeftClickPostfix(int x, int y)
    {
        if (Shop == null)
            return;

        if (
            !searchBoxCC.containsPoint(x, y)
            && !Shop.forSaleButtons.Any(fsb => fsb.containsPoint(x, y))
            && !categoryTabs.Values.Any(cct => cct.containsPoint(x, y))
        )
        {
            SearchDeactivate();
        }
    }

    public void Draw(SpriteBatch b)
    {
        searchBox.Draw(b);
        foreach (ClickableTextureComponent cct in categoryTabs.Values)
        {
            cct.draw(b);
            cct.scale = 2f;
            cct.drawItem(b, yOffset: 4);
            cct.scale = cct.baseScale;
        }
    }
}
