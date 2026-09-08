using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed class HoldToBuyOrSellContext(ShopMenu shopMenu) : IDisposable
{
    private enum BuyOrSellState
    {
        None,
        Buy,
        Sell,
    }

    private int timeoutTick = -1;

    #region buy
    private int currentItemIndex = -1;
    private int currentForSaleBtn = -1;
    #endregion

    #region sell
    private string inventoryCCName = string.Empty;
    #endregion

    private BuyOrSellState buyOrSell = BuyOrSellState.None;
    public bool IsBeingHeld => buyOrSell != BuyOrSellState.None;
    private readonly WeakReference<ShopMenu> shopMenuRef = new(shopMenu);

    public bool TryGetCurrentCC(ShopMenu shopMenu, [NotNullWhen(true)] out ClickableComponent? cc)
    {
        cc = null;
        switch (buyOrSell)
        {
            case BuyOrSellState.None:
                return false;
            case BuyOrSellState.Buy:
                if (shopMenu.currentItemIndex != currentItemIndex)
                    return false;
                if (shopMenu.currentItemIndex + currentForSaleBtn >= shopMenu.forSale.Count)
                    return false;
                if (currentForSaleBtn < 0 || shopMenu.forSale.Count <= currentForSaleBtn)
                    return false;
                cc = shopMenu.forSaleButtons[currentForSaleBtn];
                return true;
            case BuyOrSellState.Sell:
                foreach (ClickableComponent itemCC in shopMenu.inventory.inventory)
                {
                    if (itemCC.name == inventoryCCName)
                    {
                        cc = itemCC;
                        return true;
                    }
                }
                return false;
        }
        return false;
    }

    public void Press(int perRowR, Point mousePos)
    {
        if (!shopMenuRef.TryGetTarget(out ShopMenu? shopMenu) || shopMenu == null)
        {
            Cleanup();
            return;
        }
        if (shopMenu != Game1.activeClickableMenu || shopMenu.GetChildMenu() != null)
        {
            return;
        }
        // buy
        int currItemIdx = Math.Clamp(shopMenu.currentItemIndex, 0, shopMenu.forSale.Count - perRowR);
        for (int i = 0; i < shopMenu.forSaleButtons.Count; i++)
        {
            if (currItemIdx + i < shopMenu.forSale.Count && shopMenu.forSaleButtons[i].bounds.Contains(mousePos))
            {
                currentItemIndex = currItemIdx;
                currentForSaleBtn = i;
                timeoutTick = Game1.ticks + ModEntry.Config.HoldToBuyOrSellTimeout;
                buyOrSell = BuyOrSellState.Buy;
                return;
            }
        }
        // sell
        if (shopMenu.heldItem == null && !shopMenu.readOnly)
        {
            foreach (ClickableComponent cc in shopMenu.inventory.inventory)
            {
                if (!cc.bounds.Contains(mousePos))
                {
                    continue;
                }
                if (
                    int.TryParse(cc.name, out int idx)
                    && idx >= 0
                    && idx < shopMenu.inventory.actualInventory.Count
                    && shopMenu.inventory.actualInventory[idx] is Item item
                    && shopMenu.highlightItemToSell(item)
                )
                {
                    inventoryCCName = cc.name;
                    timeoutTick = Game1.ticks + ModEntry.Config.HoldToBuyOrSellTimeout;
                    buyOrSell = BuyOrSellState.Sell;
                    return;
                }
            }
        }
    }

    internal void Draw(ShopMenu shopMenu, SpriteBatch b)
    {
        if (!TryGetCurrentCC(shopMenu, out ClickableComponent? cc))
        {
            Cleanup();
            return;
        }
        Rectangle ccRect = cc.bounds;
        int margin = buyOrSell == BuyOrSellState.Buy ? 8 : 4;
        Utility.DrawSquare(
            b,
            new(
                ccRect.X + margin,
                ccRect.Y + margin,
                (
                    Game1.ticks >= timeoutTick
                        ? ccRect.Width
                        : (int)(
                            ccRect.Width
                            * (1f - ((timeoutTick - Game1.ticks) / (float)ModEntry.Config.HoldToBuyOrSellTimeout))
                        )
                )
                    - margin * 2,
                ccRect.Height - margin * 2
            ),
            0,
            backgroundColor: Color.Green * 0.4f
        );
    }

    public void Release()
    {
        if (
            !shopMenuRef.TryGetTarget(out ShopMenu? shopMenu)
            || shopMenu == null
            || !TryGetCurrentCC(shopMenu, out ClickableComponent? cc)
        )
        {
            Cleanup();
            return;
        }
        if (shopMenu != Game1.activeClickableMenu || shopMenu.GetChildMenu() != null)
        {
            return;
        }
        if (Game1.ticks >= timeoutTick)
        {
            switch (buyOrSell)
            {
                case BuyOrSellState.Buy:
                    ISalable salable = shopMenu.forSale[shopMenu.currentItemIndex + currentForSaleBtn];
                    ItemStockInformation itemStockInformation = shopMenu.itemPriceAndStock[salable];
                    int maxBuyable = Patches.ClampToMaxBuyCount(shopMenu, itemStockInformation, salable, int.MaxValue);
                    shopMenu.hoveredItem = null;
                    shopMenu.SetChildMenu(
                        new NumberPadMenu(
                            1,
                            maxBuyable,
                            (amount) => CommitBuy(shopMenu, cc, amount),
                            (menu) => RestoreFocus(menu, cc.myID),
                            Cleanup
                        )
                    );
                    break;
                case BuyOrSellState.Sell:
                    if (
                        int.TryParse(cc.name, out int idx)
                        && shopMenu.inventory.actualInventory[idx] is Item item
                        && shopMenu.highlightItemToSell(item)
                    )
                    {
                        shopMenu.hoveredItem = null;
                        shopMenu.SetChildMenu(
                            new NumberPadMenu(
                                1,
                                item.Stack,
                                (amount) => CommitSale(shopMenu, cc, amount),
                                (menu) => RestoreFocus(menu, cc.myID),
                                Cleanup
                            )
                        );
                    }
                    break;
            }
        }
        else
        {
            Patches.CheckHoldToBuy = false;
            shopMenu.receiveLeftClick(cc.bounds.Center.X, cc.bounds.Center.Y);
            Patches.CheckHoldToBuy = true;
            Cleanup();
        }
    }

    private static void RestoreFocus(IClickableMenu menu, int snapId)
    {
        menu.setCurrentlySnappedComponentTo(snapId);
        menu.snapCursorToCurrentSnappedComponent();
    }

    private static void CommitBuy(ShopMenu shopMenu, ClickableComponent cc, int amount)
    {
        if (amount == 0)
            return;
        Patches.HoldToBuyAmount = amount;
        Patches.CheckHoldToBuy = false;
        shopMenu.receiveLeftClick(cc.bounds.Center.X, cc.bounds.Center.Y);
        Patches.CheckHoldToBuy = true;
        Patches.HoldToBuyAmount = -1;
    }

    private static void CommitSale(ShopMenu shopMenu, ClickableComponent cc, int amount)
    {
        if (amount == 0)
            return;
        Patches.HoldToSellAmount = amount;
        Patches.CheckHoldToBuy = false;
        shopMenu.receiveLeftClick(cc.bounds.Center.X, cc.bounds.Center.Y);
        Patches.CheckHoldToBuy = true;
        Patches.HoldToSellAmount = -1;
    }

    private void Cleanup()
    {
        timeoutTick = -1;
        currentItemIndex = -1;
        currentForSaleBtn = -1;
        buyOrSell = BuyOrSellState.None;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
