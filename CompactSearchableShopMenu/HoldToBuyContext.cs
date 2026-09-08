using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed class HoldToBuyContext : IDisposable
{
    private int timeoutTick = -1;
    private int currentItemIndex = -1;
    private int currentForSaleBtn = -1;
    private bool playSound = false;
    private bool isBeingHeld = false;

    public bool TryGetCurrentForSaleBtn(ShopMenu shopMenu, [NotNullWhen(true)] out ClickableComponent? cc)
    {
        cc = null;
        if (!isBeingHeld)
            return false;
        if (shopMenu.currentItemIndex != currentItemIndex)
            return false;
        if (currentForSaleBtn <= 0 || shopMenu.forSale.Count <= currentForSaleBtn)
            return false;
        cc = shopMenu.forSaleButtons[currentForSaleBtn];
        return true;
    }

    public void Click(int currentItemIndex, int currentForSaleBtn, bool playSound)
    {
        this.currentItemIndex = currentItemIndex;
        this.currentForSaleBtn = currentForSaleBtn;
        this.playSound = playSound;
        timeoutTick = Game1.ticks + ModEntry.Config.HoldToBuyTimeout;
        isBeingHeld = true;
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
                ccRect.Width - 16,
                (
                    Game1.ticks >= timeoutTick
                        ? ccRect.Width
                        : (int)(
                            ccRect.Width
                            * (1f - ((timeoutTick - Game1.ticks) / (float)ModEntry.Config.HoldToBuyTimeout))
                        )
                ) - 16
            ),
            0,
            backgroundColor: Color.Green * 0.4f
        );
    }

    public void Release(ShopMenu shopMenu, int x, int y)
    {
        if (!TryGetCurrentForSaleBtn(shopMenu, out ClickableComponent? cc))
        {
            Cleanup();
            return;
        }
        if (Game1.ticks >= timeoutTick)
        {
            ModEntry.Log($"{Game1.ticks} > {timeoutTick}: open numpad");
        }
        else
        {
            ModEntry.Log($"{Game1.ticks} <= {timeoutTick}: normal buy");
            Patches.CheckHoldToBuy = false;
            shopMenu.receiveLeftClick(cc.bounds.Center.X, cc.bounds.Center.Y, playSound);
            Patches.CheckHoldToBuy = true;
        }
        Cleanup();
    }

    private void Cleanup()
    {
        timeoutTick = -1;
        currentItemIndex = -1;
        currentForSaleBtn = -1;
        isBeingHeld = false;
    }

    public void Dispose()
    {
        Cleanup();
    }
}
