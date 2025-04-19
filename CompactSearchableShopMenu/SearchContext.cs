using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed record FilterTab(ClickableTextureComponent CTC, Func<ISalable, bool> Filter);

internal sealed class SearchContext : IDisposable
{
    internal const string NO_FILTER = "NO_FILTER";
    internal const string CATEGORY_PREFIX = "category_";
    internal const string RECIPES = "recipes";
    internal static readonly string CATEGORY_SEEDS = string.Concat(CATEGORY_PREFIX, SObject.SeedsCategory.ToString());
    internal const string SEEDS_CROP = "seeds_crop";
    internal const string SEEDS_TREE = "seeds_tree";
    internal const string SEEDS_BUSH = "seeds_bush";

    internal static readonly Rectangle cursorsTabsSourceRect = new(16, 368, 16, 16);
    internal static readonly Rectangle recipeSourceRect = Game1.getSourceRectForStandardTileSheet(
        Game1.objectSpriteSheet,
        451,
        16,
        16
    );
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
    private readonly TextBox? searchBox;
    private readonly ClickableComponent searchBoxCC;
    private readonly Texture2D tabTexture;
    private readonly Rectangle tabSourceRect;
    private readonly Dictionary<string, FilterTab> filterTabs = [];
    private readonly List<string> filterTabsOrder = [];
    private string filterCurrent = NO_FILTER;

    private List<ISalable>? forSaleAll = null;

    // private readonly HashSet<string>? cropItemIds = null;
    // private readonly HashSet<string>? fruitTreesItemIds = null;
    // private readonly HashSet<string>? wildTreeItemIds = null;
    // private readonly HashSet<string>? customBushItemIds = null;

    // Filter functions

    private static bool Filter_Nop(ISalable salable) => throw new NotImplementedException();

    private static bool Filter_Recipe(ISalable salable) => salable.IsRecipe;

    private static bool Filter_Category(int category, ISalable salable) =>
        ShouldIncludeRecipe(salable) && salable is Item item && item.Category == category;

    private static bool ShouldIncludeRecipe(ISalable salable)
    {
        if (ModEntry.Config.EnableTab_Recipes)
            return !salable.IsRecipe;
        return true;
    }

    // private bool Filter_SeedCrop(ISalable salable) =>
    //     ShouldIncludeRecipe(salable) && salable is Item item && (cropItemIds?.Contains(item.ItemId) ?? false);

    // private bool Filter_SeedTree(ISalable salable) =>
    //     ShouldIncludeRecipe(salable)
    //     && salable is Item item
    //     && ((fruitTreesItemIds?.Contains(item.ItemId) ?? false) || (wildTreeItemIds?.Contains(item.ItemId) ?? false));

    // private bool Filter_SeedBush(ISalable salable) =>
    //     ShouldIncludeRecipe(salable)
    //     && salable is Item item
    //     && (customBushItemIds?.Contains(item.QualifiedItemId) ?? false);
    private static bool Filter_SeedCrop(ISalable salable) =>
        ShouldIncludeRecipe(salable)
        && salable is SObject obj
        && obj.Category == SObject.SeedsCategory
        && !(obj.IsTeaSapling() || obj.IsWildTreeSapling() || obj.IsFruitTreeSapling());

    private static bool Filter_SeedTree(ISalable salable) =>
        ShouldIncludeRecipe(salable) && salable is SObject obj && (obj.IsWildTreeSapling() || obj.IsFruitTreeSapling());

    private static bool Filter_SeedBush(ISalable salable) =>
        ShouldIncludeRecipe(salable) && salable is SObject obj && obj.IsTeaSapling();

    internal SearchContext(ShopMenu shopMenu)
    {
        this.shopMenu = new(shopMenu);
        if (Shop == null)
            throw new InvalidDataException();

        tabTexture = Game1.mouseCursors;
        tabSourceRect = cursorsTabsSourceRect;

        searchBoxCC = new(Rectangle.Empty, "SEARCH");
        if (ModEntry.Config.EnableSearch)
        {
            searchBox = new(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null,
                Game1.smallFont,
                Game1.textColor
            );
            searchBox.OnEnterPressed += OnEnter;
            searchBox.Text = I18n.Placeholder_Search();
        }

        if (!shopMenu.tabButtons.Any())
        {
            if (ModEntry.Config.EnableTab_Category)
            {
                foreach (var sale in Shop.forSale)
                {
                    if (sale is not Item item || item.IsRecipe)
                        continue;
                    int category = item.Category;
                    string categoryKey = string.Concat(CATEGORY_PREFIX, item.Category.ToString());
                    if (!filterTabs.ContainsKey(categoryKey))
                    {
                        filterTabs[categoryKey] = new(
                            new(
                                item.Category.ToString(),
                                Rectangle.Empty,
                                "",
                                categoryKey,
                                tabTexture,
                                tabSourceRect,
                                4f,
                                false
                            )
                            {
                                item = item,
                            },
                            (salable) => Filter_Category(category, salable)
                        );
                    }
                }
                filterTabsOrder.AddRange(filterTabs.Keys);
            }

            if (ModEntry.Config.EnableTab_DetailedSeeds)
            {
                if (filterTabs.ContainsKey(CATEGORY_SEEDS))
                {
                    filterTabs.Remove(CATEGORY_SEEDS);
                    filterTabsOrder.Remove(CATEGORY_SEEDS);
                }
                // bush
                if (Shop.forSale.FirstOrDefault(Filter_SeedBush) is Item seedBushItem)
                {
                    filterTabs[SEEDS_BUSH] = new(
                        new(RECIPES, Rectangle.Empty, "", SEEDS_TREE, tabTexture, tabSourceRect, 4f, false)
                        {
                            item = seedBushItem,
                        },
                        Filter_SeedBush
                    );
                    filterTabsOrder.Insert(0, SEEDS_BUSH);
                }
                // tree
                if (Shop.forSale.FirstOrDefault(Filter_SeedTree) is Item seedTreeItem)
                {
                    filterTabs[SEEDS_TREE] = new(
                        new(RECIPES, Rectangle.Empty, "", SEEDS_TREE, tabTexture, tabSourceRect, 4f, false)
                        {
                            item = seedTreeItem,
                        },
                        Filter_SeedTree
                    );
                    filterTabsOrder.Insert(0, SEEDS_TREE);
                }
                // crop
                if (Shop.forSale.FirstOrDefault(Filter_SeedCrop) is Item seedCropItem)
                {
                    filterTabs[SEEDS_CROP] = new(
                        new(RECIPES, Rectangle.Empty, "", SEEDS_CROP, tabTexture, tabSourceRect, 4f, false)
                        {
                            item = seedCropItem,
                        },
                        Filter_SeedCrop
                    );
                    filterTabsOrder.Insert(0, SEEDS_CROP);
                }
            }

            if (ModEntry.Config.EnableTab_Recipes)
            {
                if (Shop.forSale.FirstOrDefault(Filter_Recipe) is Item recipeItem)
                {
                    filterTabs[RECIPES] = new(
                        new(RECIPES, Rectangle.Empty, "", RECIPES, tabTexture, tabSourceRect, 4f, false)
                        {
                            item = recipeItem,
                        },
                        Filter_Recipe
                    );
                    filterTabsOrder.Add(RECIPES);
                }
            }

            if (filterTabsOrder.Count > 1)
            {
                ModEntry.Log($"Setup tabs for {Shop.ShopId}: {string.Join(", ", filterTabsOrder)}");
                filterTabsOrder.Insert(0, NO_FILTER);
                filterTabs[NO_FILTER] = new(
                    new(NO_FILTER.ToString(), Rectangle.Empty, "", NO_FILTER, tabTexture, tabSourceRect, 4f, false),
                    Filter_Nop
                );
            }
            else
            {
                filterTabsOrder.Clear();
                filterTabs.Clear();
            }
        }

        Reposition();
        if (searchBox != null)
        {
            Game1.keyboardDispatcher.Subscriber = searchBox;
            searchBox.Selected = false;
        }
    }

    public Rectangle SearchBoxRect()
    {
        return new(
            Shop!.xPositionOnScreen,
            Shop.yPositionOnScreen
                + Shop.height
                - Shop.inventory.height
                - 12
                + 17 * 4
                + 4
                + (ModEntry.HasMod_BiggerBackpack ? 64 : 0),
            240,
            56
        );
    }

    public void Reposition()
    {
        if (Shop == null)
            return;

        if (searchBox != null)
        {
            Rectangle searchBoxRect = SearchBoxRect();
            searchBox.X = searchBoxRect.X;
            searchBox.Y = searchBoxRect.Y;
            searchBox.Width = searchBoxRect.Width;
            searchBox.Height = searchBoxRect.Height;
            searchBoxCC.bounds = searchBoxRect;
        }

        int n = 0;
        foreach (string filter in filterTabsOrder)
        {
            FilterTab tab = filterTabs[filter];
            ClickableTextureComponent ctc = tab.CTC;
            ctc.bounds.X = Shop.xPositionOnScreen + n * tabSourceRect.Width * 4 + 20;
            ctc.bounds.Y = Shop.yPositionOnScreen - tabSourceRect.Height * 4 + 8;
            if (filter == filterCurrent)
            {
                ctc.bounds.Y += TAB_OFFSET;
            }
            ctc.bounds.Width = tabSourceRect.Width * 4;
            ctc.bounds.Height = tabSourceRect.Height * 4;
            n++;
        }

        filterCurrent = NO_FILTER;
    }

    public void Dispose()
    {
        forSaleAll = null;
        filterTabsOrder.Clear();
        filterTabs.Clear();
        if (searchBox != null)
        {
            searchBox.Selected = false;
            if (Game1.keyboardDispatcher.Subscriber == searchBox)
                Game1.keyboardDispatcher.Subscriber = null;
        }
    }

    public void OnEnter(TextBox sender) => DoSearch();

    public bool OnKeyPress(Keys key)
    {
        if (searchBox?.Selected ?? false)
        {
            DoSearch();
            return false;
        }
        return true;
    }

    public bool OnGamePadButton(Buttons button)
    {
        if (
            Shop == null
            || filterTabsOrder.Count == 0
            || (button != Buttons.RightShoulder && button != Buttons.LeftShoulder)
        )
        {
            return true;
        }

        if (filterTabs.TryGetValue(filterCurrent, out FilterTab? tab))
        {
            tab.CTC.bounds.Y -= TAB_OFFSET;
        }
        int nextIdx = filterTabsOrder.IndexOf(filterCurrent);
        if (button == Buttons.RightShoulder)
        {
            nextIdx++;
            if (nextIdx == filterTabsOrder.Count)
                nextIdx = 0;
        }
        else if (button == Buttons.LeftShoulder)
        {
            nextIdx--;
            if (nextIdx == -1)
                nextIdx = filterTabsOrder.Count - 1;
        }
        filterCurrent = filterTabsOrder[nextIdx];
        if (filterTabs.TryGetValue(filterCurrent, out tab))
        {
            tab.CTC.bounds.Y += TAB_OFFSET;
        }
        DoSearch();

        return false;
    }

    public void GamepadToggleSearch()
    {
        if (Shop == null || searchBox == null)
            return;

        if (searchBox.Selected)
        {
            SearchDeactivate();
            Shop.snapToDefaultClickableComponent();
        }
        else
        {
            SearchActivate();
            searchBoxCC.snapMouseCursorToCenter();
        }
    }

    public void DoSearch()
    {
        if (Shop == null)
            return;

        forSaleAll ??= Shop.forSale;
        IEnumerable<ISalable> forSale = forSaleAll;
        if (filterCurrent != NO_FILTER && filterTabs.TryGetValue(filterCurrent, out FilterTab? tab))
        {
            forSale = forSale.Where(tab.Filter);
        }
        if (searchBox?.Selected ?? false)
        {
            string searchText = searchBox.Text;
            if (!string.IsNullOrEmpty(searchText))
                forSale = forSale.Where(fs => fs.DisplayName.ContainsIgnoreCase(searchText));
        }
        Shop.forSale = forSale.ToList();
        Shop.currentItemIndex = 0;
        Patches.setScrollBarToCurrentIndexMethod?.Invoke(Shop, []);
    }

    internal void SearchActivate()
    {
        if (Shop == null || searchBox == null)
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

        if (searchBox != null && searchBox.Selected)
        {
            searchBox.Text = I18n.Placeholder_Search();
            searchBox.Selected = false;
        }
        if (forSaleAll != null)
        {
            if (filterCurrent == NO_FILTER)
            {
                Shop.currentItemIndex = Math.Min(Shop.currentItemIndex, forSaleAll.Count - Patches.PerRowR);
                Patches.setScrollBarToCurrentIndexMethod?.Invoke(Shop, []);
                Shop.forSale = forSaleAll;
                forSaleAll = null;
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

        if (searchBox != null && searchBoxCC.containsPoint(x, y))
        {
            SearchActivate();
        }
        else
        {
            string? clickedFilter = null;
            foreach ((string category, FilterTab tab) in filterTabs)
            {
                if (tab.CTC.containsPoint(x, y))
                {
                    tab.CTC.bounds.Y += TAB_OFFSET;
                    clickedFilter = category;
                    break;
                }
            }
            if (clickedFilter != null)
            {
                if (filterTabs.TryGetValue(filterCurrent, out FilterTab? tab))
                {
                    tab.CTC.bounds.Y -= TAB_OFFSET;
                }
                string prevFilter = filterCurrent;
                filterCurrent = clickedFilter;
                if (filterCurrent != prevFilter)
                {
                    DoSearch();
                }
            }
        }
    }

    internal void OnLeftClickPostfix(int x, int y)
    {
        if (Shop == null)
        {
            forSaleAll = null;
            return;
        }
        forSaleAll?.RemoveWhere(fsa => !Shop.itemPriceAndStock.ContainsKey(fsa));
        if (
            !(searchBox != null && searchBoxCC.containsPoint(x, y))
            && !Shop.forSaleButtons.Any(fsb => fsb.containsPoint(x, y))
            && !filterTabs.Values.Any(tab => tab.CTC.containsPoint(x, y))
        )
        {
            SearchDeactivate();
        }
    }

    public void Draw(SpriteBatch b)
    {
        searchBox?.Draw(b);
        foreach (FilterTab tab in filterTabs.Values)
        {
            ClickableTextureComponent ctc = tab.CTC;

            if (!ctc.visible)
                continue;
            ctc.draw(b);
            if (ctc.item == null)
                continue;
            if (ctc.item.IsRecipe)
            {
                b.Draw(
                    Game1.objectSpriteSheet,
                    new(ctc.bounds.X + 3, ctc.bounds.Y + 6),
                    recipeSourceRect,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    3f,
                    SpriteEffects.None,
                    0.9f
                );
            }
            else
            {
                ctc.item.drawInMenu(
                    b,
                    new(ctc.bounds.X, ctc.bounds.Y + 6),
                    0.75f,
                    1f,
                    0.9f,
                    StackDrawType.Hide,
                    Color.White,
                    false
                );
            }
        }
    }
}
