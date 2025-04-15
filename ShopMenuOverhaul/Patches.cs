using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace ShopMenuOverhaul;

internal static class Patches
{
    internal const int ROW = 4;
    internal static PerScreen<int> perRow = new();

    internal static void Patch()
    {
        perRow.Value = 3;
        Harmony harmony = new(ModEntry.ModId);

        // not sure why this is even needed in the first place, skip prefix so it doesn't mess with my neighbour ids
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.updateSaleButtonNeighbors)),
            prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_updateSaleButtonNeighbors_Prefix))
        );
        // redo for sale ClickableComponent
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), "Initialize"),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_Initialize_Transpiler))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.gameWindowSizeChanged)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_Initialize_Transpiler))
        );
        // scrolling snap behavior
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), "downArrowPressed"),
            prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_downArrowPressed_Prefix))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), "upArrowPressed"),
            prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_upArrowPressed_Prefix))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), "customSnapBehavior"),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_customSnapBehavior_Transpiler))
        );
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveScrollWheelAction)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_customSnapBehavior_Transpiler))
        );
        // draw (do not draw the text cus it's unreadable lol)
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.draw)),
            transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_draw_Transpiler))
        );
    }

    private static bool ShopMenu_updateSaleButtonNeighbors_Prefix()
    {
        return false;
    }

    private static void ShopMenu_downArrowPressed_Prefix(ShopMenu __instance)
    {
        __instance.currentItemIndex += perRow.Value - 1;
    }

    private static void ShopMenu_upArrowPressed_Prefix(ShopMenu __instance)
    {
        __instance.currentItemIndex -= perRow.Value - 1;
    }

    private static IEnumerable<CodeInstruction> ShopMenu_customSnapBehavior_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_0015: ldfld class [System.Collections]System.Collections.Generic.List`1<class StardewValley.ISalable> StardewValley.Menus.ShopMenu::forSale
            // IL_001a: callvirt instance int32 class [System.Collections]System.Collections.Generic.List`1<class StardewValley.ISalable>::get_Count()
            // IL_001f: ldc.i4.4
            // IL_0020: sub
            matcher
                .MatchEndForward(
                    [
                        new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(ShopMenu), nameof(ShopMenu.forSale))),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(List<ISalable>), nameof(List<ISalable>.Count))
                        ),
                        new(OpCodes.Ldc_I4_4),
                        new(OpCodes.Sub),
                    ]
                )
                .ThrowIfNotMatch("Failed to find 'forSale.Count - 4'")
                .Insert(
                    [
                        new(OpCodes.Ldsfld, AccessTools.DeclaredField(typeof(Patches), nameof(perRow))),
                        new(
                            OpCodes.Callvirt,
                            AccessTools.PropertyGetter(typeof(PerScreen<int>), nameof(PerScreen<int>.Value))
                        ),
                        new(OpCodes.Mul),
                    ]
                );
            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in ShopMenu_customSnapBehavior_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    private static void MakeGridLikeSaleButtons(ShopMenu shopMenu)
    {
        int perRowV = shopMenu.itemPriceAndStock.Values.Any(stockInfo =>
            stockInfo.Price > 0 || stockInfo.TradeItemCount > 0
        )
            ? ModEntry.Config.ShopItemPerRow
            : ModEntry.Config.DresserItemPerRow;
        perRowV = Math.Clamp(perRowV, 1, 9);
        perRow.Value = perRowV;
        List<ClickableComponent> newForSaleButtons = [];
        int idx = 0;
        foreach (ClickableComponent saleButton in shopMenu.forSaleButtons)
        {
            Rectangle saleBounds = saleButton.bounds;
            int width = saleBounds.Width / perRowV;
            for (int i = 0; i < perRowV; i++)
            {
                Rectangle newBounds = new(saleBounds.X + width * i, saleBounds.Y, width, saleBounds.Height);
                int myID = 3546 + idx;
                newForSaleButtons.Add(
                    new ClickableComponent(newBounds, idx.ToString() ?? "")
                    {
                        myID = myID,
                        upNeighborID = idx > perRowV ? myID - perRowV : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                        rightNeighborID = i == perRowV - 1 ? ShopMenu.region_upArrow : myID + 1,
                        downNeighborID =
                            idx < perRowV * (ROW - 1) ? myID + perRowV : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
                        leftNeighborID = i == 0 ? ClickableComponent.SNAP_AUTOMATIC : myID - 1,
                        fullyImmutable = true,
                    }
                );
                idx++;
            }
        }
        shopMenu.forSaleButtons.Clear();
        shopMenu.forSaleButtons.AddRange(newForSaleButtons);
    }

    private static IEnumerable<CodeInstruction> ShopMenu_Initialize_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);
            // IL_031c: ldfld class [System.Collections] System.Collections.Generic.List`1<class StardewValley.Menus.ShopMenu/ShopTabClickableTextureComponent> StardewValley.Menus.ShopMenu::tabButtons
            // IL_0321: callvirt instance int32 class [System.Collections] System.Collections.Generic.List`1<class StardewValley.Menus.ShopMenu/ShopTabClickableTextureComponent>::get_Count()
            // IL_0326: ldc.i4.0
            // IL_0327: ble.s IL_0361
            CodeMatch[] matches =
            [
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(ShopMenu), nameof(ShopMenu.tabButtons))),
                new(
                    OpCodes.Callvirt,
                    AccessTools.PropertyGetter(
                        typeof(List<ShopMenu.ShopTabClickableTextureComponent>),
                        nameof(List<ShopMenu.ShopTabClickableTextureComponent>.Count)
                    )
                ),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Ble_S),
            ];
            matcher.MatchStartForward(matches).ThrowIfNotMatch("Failed to find 'tabButtons.Count > 0'");
            matcher.InsertAndAdvance(
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(MakeGridLikeSaleButtons))),
                ]
            );
            matcher.Advance(matches.Length - 1);
            Console.WriteLine(matcher.Instruction);
            Label lbl = (Label)matcher.Operand;
            Console.WriteLine(lbl);
            matcher.Advance(1).Insert([new(OpCodes.Br, lbl)]);

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in ShopMenu_Initialize_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }

    public static void MaybeDrawString(
        SpriteBatch b,
        string s,
        int x,
        int y,
        int characterPosition = 999999,
        int width = -1,
        int height = 999999,
        float alpha = 1f,
        float layerDepth = 0.88f,
        bool junimoText = false,
        int drawBGScroll = -1,
        string placeHolderScrollWidthText = "",
        Color? color = null,
        SpriteText.ScrollTextAlignment scroll_text_alignment = SpriteText.ScrollTextAlignment.Left
    )
    {
        if (perRow.Value <= 2)
        {
            SpriteText.drawString(
                b,
                s,
                x,
                y,
                characterPosition,
                width,
                height,
                alpha,
                layerDepth,
                junimoText,
                drawBGScroll,
                placeHolderScrollWidthText,
                color,
                scroll_text_alignment
            );
        }
    }

    private static IEnumerable<CodeInstruction> ShopMenu_draw_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        try
        {
            CodeMatcher matcher = new(instructions, generator);

            var nopDrawString = AccessTools.DeclaredMethod(typeof(Patches), nameof(MaybeDrawString));
            matcher.MatchStartForward(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
            );
            matcher.Operand = nopDrawString;
            matcher.MatchStartForward(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
            );
            matcher.Operand = nopDrawString;

            return matcher.Instructions();
        }
        catch (Exception err)
        {
            ModEntry.Log($"Error in ShopMenu_draw_Transpiler:\n{err}", LogLevel.Error);
            return instructions;
        }
    }
}
