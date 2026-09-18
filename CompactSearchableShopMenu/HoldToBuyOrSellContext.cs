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
    private int buyStepTick = -1;
    private int buyStep = 0;

    #region buy
    private int currentItemIndex = -1;
    private int currentForSaleBtn = -1;
    private int curretMaxBuyable;
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

    public void Press(int maxItemIndex, Point mousePos)
    {
        if (!shopMenuRef.TryGetTarget(out ShopMenu? shopMenu) || shopMenu == null)
        {
            Reset();
            return;
        }
        if (shopMenu != Game1.activeClickableMenu || shopMenu.GetChildMenu() != null)
        {
            return;
        }
        // buy
        int currItemIdx = Math.Clamp(shopMenu.currentItemIndex, 0, maxItemIndex);
        for (int i = 0; i < shopMenu.forSaleButtons.Count; i++)
        {
            if (currItemIdx + i < shopMenu.forSale.Count && shopMenu.forSaleButtons[i].bounds.Contains(mousePos))
            {
                ISalable salable = shopMenu.forSale[shopMenu.currentItemIndex + i];
                ItemStockInformation itemStockInformation = shopMenu.itemPriceAndStock[salable];
                int maxBuyable = Patches.ClampToMaxBuyCount(shopMenu, itemStockInformation, salable, int.MaxValue);
                if (maxBuyable > 0)
                {
                    currentItemIndex = currItemIdx;
                    currentForSaleBtn = i;
                    curretMaxBuyable = maxBuyable;
                    timeoutTick = Game1.ticks + ModEntry.Config.HoldToBuyTimeout;
                    buyStepTick = timeoutTick + (ModEntry.Config.HoldToBuyTimeout / 2);
                    buyStep = 0;
                    buyOrSell = BuyOrSellState.Buy;
                    return;
                }
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
                    timeoutTick = Game1.ticks + ModEntry.Config.HoldToSellTimeout;
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
            Reset();
            return;
        }
        Rectangle ccRect = cc.bounds;
        int margin = buyOrSell == BuyOrSellState.Buy ? 8 : 4;
        if (shopMenu.GetChildMenu() == null && Game1.ticks == buyStepTick && buyStep < 3)
        {
            buyStep++;
            if (buyStep < 3)
                buyStepTick += (ModEntry.Config.HoldToBuyTimeout / 2);
        }
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
                            * (1f - ((timeoutTick - Game1.ticks) / (float)ModEntry.Config.HoldToBuyTimeout))
                        )
                )
                    - margin * 2,
                ccRect.Height - margin * 2
            ),
            0,
            backgroundColor: Color.Green * 0.4f
        );
        if (buyStep > 0)
        {
            Utility.drawTextWithShadow(
                b,
                GetInitialBuyCount().ToString(),
                Game1.tinyFont,
                new Vector2(cc.bounds.Left + 24, cc.bounds.Top + 4),
                Game1.textColor,
                layerDepth: 1f
            );
        }
    }

    public void Release()
    {
        if (
            !shopMenuRef.TryGetTarget(out ShopMenu? shopMenu)
            || shopMenu == null
            || !TryGetCurrentCC(shopMenu, out ClickableComponent? cc)
        )
        {
            Reset();
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
                    SetChildMenu(
                        shopMenu,
                        new NumberPadMenu(
                            GetInitialBuyCount(),
                            curretMaxBuyable,
                            (amount) => CommitBuy(shopMenu, cc, amount),
                            (menu) => RestoreFocus(menu, cc.myID),
                            Reset
                        )
                    );
                    return;
                case BuyOrSellState.Sell:
                    if (
                        int.TryParse(cc.name, out int idx)
                        && shopMenu.inventory.actualInventory[idx] is Item item
                        && shopMenu.highlightItemToSell(item)
                    )
                    {
                        SetChildMenu(
                            shopMenu,
                            new NumberPadMenu(
                                item.Stack,
                                item.Stack,
                                (amount) => CommitSale(shopMenu, cc, amount),
                                (menu) => RestoreFocus(menu, cc.myID),
                                Reset
                            )
                        );
                        return;
                    }
                    break;
            }
            Reset();
            return;
        }
        else
        {
            Patches.CheckHoldToBuy = false;
            shopMenu.receiveLeftClick(cc.bounds.Center.X, cc.bounds.Center.Y);
            Patches.CheckHoldToBuy = true;
            Reset();
        }

        static void SetChildMenu(ShopMenu shopMenu, NumberPadMenu numberPadMenu)
        {
            shopMenu.hoverText = "";
            shopMenu.hoveredItem = null;
            shopMenu.SetChildMenu(numberPadMenu);
        }
    }

    private int GetInitialBuyCount()
    {
        return buyStep switch
        {
            3 => ModEntry.Config.StackCount_999,
            2 => ModEntry.Config.StackCount_25,
            1 => ModEntry.Config.StackCount_5,
            _ => 0,
        };
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
        if (shopMenu.heldItem != null)
        {
            Game1.player.addItemToInventoryBool(shopMenu.heldItem as Item);
            shopMenu.heldItem = null;
        }
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

    public void Reset()
    {
        timeoutTick = -1;
        buyStepTick = -1;
        buyStep = 0;
        currentItemIndex = -1;
        currentForSaleBtn = -1;
        curretMaxBuyable = -1;
        inventoryCCName = string.Empty;
        buyOrSell = BuyOrSellState.None;
    }

    public void Dispose()
    {
        Reset();
    }
}
