using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed class HoldToBuyOrSellContext(ShopMenu shopMenu) : IDisposable
{
    private int timeoutTick = -1;
    private int currentItemIndex = -1;
    private int currentForSaleBtn = -1;
    public bool IsBeingHeld { get; private set; } = false;
    private readonly WeakReference<ShopMenu> shopMenuRef = new(shopMenu);

    public bool TryGetCurrentForSaleBtn(ShopMenu shopMenu, [NotNullWhen(true)] out ClickableComponent? cc)
    {
        cc = null;
        if (!IsBeingHeld)
            return false;
        if (shopMenu.currentItemIndex != currentItemIndex)
            return false;
        if (shopMenu.currentItemIndex + currentForSaleBtn >= shopMenu.forSale.Count)
            return false;
        if (currentForSaleBtn < 0 || shopMenu.forSale.Count <= currentForSaleBtn)
            return false;
        cc = shopMenu.forSaleButtons[currentForSaleBtn];
        return true;
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
        int currentItemIndex = Math.Clamp(shopMenu.currentItemIndex, 0, shopMenu.forSale.Count - perRowR);
        for (int i = 0; i < shopMenu.forSaleButtons.Count; i++)
        {
            if (currentItemIndex + i < shopMenu.forSale.Count && shopMenu.forSaleButtons[i].bounds.Contains(mousePos))
            {
                this.currentItemIndex = currentItemIndex;
                this.currentForSaleBtn = i;
                timeoutTick = Game1.ticks + ModEntry.Config.HoldToBuyTimeout;
                IsBeingHeld = true;
                return;
            }
        }
    }

    internal void Draw(ShopMenu shopMenu, SpriteBatch b)
    {
        if (!TryGetCurrentForSaleBtn(shopMenu, out ClickableComponent? cc))
        {
            Cleanup();
            return;
        }
        Rectangle ccRect = cc.bounds;
        Utility.DrawSquare(
            b,
            new(
                ccRect.X + 8,
                ccRect.Y + 8,
                (
                    Game1.ticks >= timeoutTick
                        ? ccRect.Width
                        : (int)(
                            ccRect.Width
                            * (1f - ((timeoutTick - Game1.ticks) / (float)ModEntry.Config.HoldToBuyTimeout))
                        )
                ) - 16,
                ccRect.Height - 16
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
            || !TryGetCurrentForSaleBtn(shopMenu, out ClickableComponent? cc)
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
            IsBeingHeld = false;
            ISalable salable = shopMenu.forSale[shopMenu.currentItemIndex + currentForSaleBtn];
            ItemStockInformation itemStockInformation = shopMenu.itemPriceAndStock[salable];
            int maxBuyable = Patches.ClampToMaxBuyCount(shopMenu, itemStockInformation, salable, int.MaxValue);
            shopMenu.hoveredItem = null;
            shopMenu.SetChildMenu(
                new NumberPadMenu(
                    1,
                    maxBuyable,
                    (amount) => CommitPurchase(shopMenu, cc, amount),
                    (menu) => RestoreFocus(menu, cc.myID),
                    Cleanup
                )
            );
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

    private void CommitPurchase(ShopMenu shopMenu, ClickableComponent cc, int buyAmount)
    {
        Patches.HoldToBuyAmount = buyAmount;
        Patches.CheckHoldToBuy = false;
        shopMenu.receiveLeftClick(cc.bounds.Center.X, cc.bounds.Center.Y);
        Patches.CheckHoldToBuy = true;
        Patches.HoldToBuyAmount = -1;
        Cleanup();
    }

    private void Cleanup()
    {
        timeoutTick = -1;
        currentItemIndex = -1;
        currentForSaleBtn = -1;
        IsBeingHeld = false;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
