using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal static class Patches
{
    private static readonly PerScreen<int> perRow = new();
    private static readonly PerScreen<int> perRowR = new();
    internal static int PerRowV => perRow.Value;
    internal static int PerRowR => perRowR.Value;

    internal static void SetPerRow(int perRowV, int forSaleCount)
    {
        perRow.Value = perRowV;
        int remainder = forSaleCount % perRowV;
        if (remainder == 0)
            remainder = perRowV * ShopMenu.itemsPerPage;
        else
            remainder += (ShopMenu.itemsPerPage - 1) * perRowV;
        perRowR.Value = remainder;
    }

    private static readonly PerScreen<SearchContext?> searchCtx = new();
    private static SearchContext? SearchContext
    {
        get => searchCtx.Value;
        set
        {
            searchCtx.Value?.Dispose();
            searchCtx.Value = value;
        }
    }

    internal static MethodInfo setScrollBarToCurrentIndexMethod = AccessTools.DeclaredMethod(
        typeof(ShopMenu),
        "setScrollBarToCurrentIndex"
    );

    internal static bool Success_Grid = true;
    internal static bool Success_Scroll = true;
    internal static bool Success_DrawStrong = true;
    internal static bool Success_DrawWeak = true;
    internal static bool Success_StackCount = true;
    internal static bool Success_Search = true;

    internal static void Patch(IModHelper help, Harmony harmony)
    {
        perRow.Value = 3;
        perRowR.Value = 4;
        help.Events.Player.Warped += OnWarped;
        help.Events.Input.ButtonsChanged += OnButtonsChanged;

        try
        {
            // stop rebuilding neighbor ids all the time
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
        }
        catch (Exception ex)
        {
            Success_Grid = false;
            ModEntry.Log("Failed to apply grid patches, shop will not be displayed as grid.", LogLevel.Warn);
            ModEntry.Log(ex.ToString());
        }

        try
        {
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
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_replaceForSaleCountMin4_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.leftClickHeld)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_leftClickHeld_Transpiler))
            );
            harmony.Patch(
                original: setScrollBarToCurrentIndexMethod,
                prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_setScrollBarToCurrentIndex_Prefix)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_replaceForSaleCountMin4_Transpiler))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_replaceForSaleCountMin4_Transpiler))
            );
        }
        catch (Exception ex)
        {
            Success_Scroll = false;
            ModEntry.LogOnce("Failed to apply scroll patches, scroll will progress one item at a time.", LogLevel.Warn);
            ModEntry.Log(ex.ToString());
        }

        try
        {
            // draw (strong, adjusts text font)
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.draw)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_draw_Transpiler_strong))
                {
                    priority = Priority.First,
                }
            );
        }
        catch (Exception ex)
        {
            Success_DrawStrong = false;
            ModEntry.LogOnce("Failed to apply draw patches (strong), trying weaker patches.", LogLevel.Warn);
            ModEntry.Log(ex.ToString());
            try
            {
                // draw (weak, only turn off display name)
                harmony.Patch(
                    original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.draw)),
                    transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_draw_Transpiler_weak))
                    {
                        priority = Priority.First,
                    }
                );
            }
            catch (Exception ex2)
            {
                Success_DrawWeak = false;
                ModEntry.LogOnce(
                    "Failed to apply draw patches (weak), shop items will display strangely.",
                    LogLevel.Warn
                );
                ModEntry.Log(ex2.ToString());
            }
        }

        // search box
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), "Initialize"),
                postfix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_Initialize_Postfix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.gameWindowSizeChanged)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_gameWindowSizeChanged_Postfix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_receiveLeftClick_Prefix)),
                postfix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_receiveLeftClick_Postfix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveKeyPress)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_receiveKeyPress_Prefix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveGamePadButton)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_receiveGamePadButton_Prefix))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.drawCurrency)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_drawCurrency_Prefix))
            );
        }
        catch (Exception ex)
        {
            Success_Search = false;
            ModEntry.LogOnce("Failed to apply search patches, no search box available.", LogLevel.Warn);
            ModEntry.Log(ex.ToString());
        }

        // stack count adjustment
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_replaceStackCounts_transpiler))
            );
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.receiveRightClick)),
                transpiler: new HarmonyMethod(typeof(Patches), nameof(ShopMenu_replaceStackCounts_transpiler))
            );
        }
        catch (Exception ex)
        {
            Success_StackCount = false;
            ModEntry.LogOnce("Failed to apply stack count patch, using vanilla buy 25 on Shift+Ctrl.", LogLevel.Warn);
            ModEntry.Log(ex.ToString());
        }
    }

    private static void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (e.Pressed.Contains(SButton.LeftStick))
        {
            SearchContext?.GamepadToggleSearch();
        }
    }

    private static void ShopMenu_setScrollBarToCurrentIndex_Prefix(ShopMenu __instance)
    {
        SetPerRow(perRow.Value, __instance.forSale.Count);
    }

    private static void OnWarped(object? sender, WarpedEventArgs e)
    {
        // a silly event to dispose search context on, for lookup anything reasons
        SearchContext = null;
    }

    private static void ShopMenu_Initialize_Postfix(ShopMenu __instance)
    {
        SearchContext = null;
        if (ModEntry.Config.EnableSearchAndFilters)
        {
            try
            {
                SearchContext = new(__instance);
            }
            catch
            {
                ModEntry.Log($"Failed to initialize search context for {__instance.ShopId}.", LogLevel.Warn);
                SearchContext = null;
            }
        }
    }

    private static void ShopMenu_gameWindowSizeChanged_Postfix()
    {
        SearchContext?.Reposition();
    }

    private static void ShopMenu_receiveLeftClick_Prefix(int x, int y)
    {
        SearchContext?.OnLeftClickPrefix(x, y);
    }

    private static void ShopMenu_receiveLeftClick_Postfix(int x, int y)
    {
        SearchContext?.OnLeftClickPostfix(x, y);
    }

    private static bool ShopMenu_receiveKeyPress_Prefix(Keys key)
    {
        return SearchContext?.OnKeyPress(key) ?? true;
    }

    private static bool ShopMenu_receiveGamePadButton_Prefix(Buttons button)
    {
        return SearchContext?.OnGamePadButton(button) ?? true;
    }

    private static void ShopMenu_drawCurrency_Prefix(SpriteBatch b)
    {
        SearchContext?.Draw(b);
    }

    private static int LeftClickHeldIndex(int originalValue, int y, Rectangle scrollBarRunner, ShopMenu shopMenu)
    {
        int perRowV = perRow.Value;
        if (perRowV == 1)
            return originalValue;
        float percent = (float)(y - scrollBarRunner.Y) / scrollBarRunner.Height;
        int forSaleCount = shopMenu.forSale.Count;
        int perRowRV = perRowR.Value;
        return Math.Min(
            Math.Max(0, forSaleCount - perRowRV),
            Math.Max(0, (int)(MathF.Ceiling((float)(forSaleCount - perRowRV) / perRowV) * percent) * perRowV)
        );
    }

    private static IEnumerable<CodeInstruction> ShopMenu_leftClickHeld_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        // float num = (float)(y - scrollBarRunner.Y) / (float)scrollBarRunner.Height;
        // currentItemIndex = Math.Min(Math.Max(0, forSale.Count - 4), Math.Max(0, (int)((float)forSale.Count * num)));
        CodeMatcher matcher = new(instructions, generator);
        matcher
            .MatchEndForward(
                [
                    new(
                        OpCodes.Call,
                        AccessTools.DeclaredMethod(typeof(Math), nameof(Math.Min), [typeof(int), typeof(int)])
                    ),
                    new(OpCodes.Stfld, AccessTools.DeclaredField(typeof(ShopMenu), nameof(ShopMenu.currentItemIndex))),
                ]
            )
            .ThrowIfNotMatch("Failed to find 'currentItemIndex = Math.Min'")
            .InsertAndAdvance(
                [
                    new(OpCodes.Ldarg_2),
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(ShopMenu), "scrollBarRunner")),
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(LeftClickHeldIndex))),
                ]
            );

        return matcher.Instructions();
    }

    private static bool ShopMenu_updateSaleButtonNeighbors_Prefix()
    {
        return false;
    }

    private static void ShopMenu_downArrowPressed_Prefix(ShopMenu __instance)
    {
        __instance.currentItemIndex += perRow.Value - 1;
        if (__instance.currentItemIndex >= __instance.forSale.Count - 1)
            __instance.currentItemIndex = __instance.forSale.Count - 1;
    }

    private static void ShopMenu_upArrowPressed_Prefix(ShopMenu __instance)
    {
        __instance.currentItemIndex -= perRow.Value - 1;
        if (__instance.currentItemIndex == 0)
            __instance.currentItemIndex = 1;
    }

    private static IEnumerable<CodeInstruction> ShopMenu_replaceForSaleCountMin4_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
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
            .Repeat(
                (match) =>
                {
                    match.Advance(-1);
                    match.Opcode = OpCodes.Call;
                    match.Operand = AccessTools.PropertyGetter(typeof(Patches), nameof(PerRowR));
                    match.Advance(2);
                }
            );
        return matcher.Instructions();
    }

    internal static IEnumerable<CodeInstruction> ShopMenu_customSnapBehavior_Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        instructions = ShopMenu_replaceForSaleCountMin4_Transpiler(instructions, generator);
        CodeMatcher matcher = new(instructions, generator);
        matcher.MatchEndForward([new(OpCodes.Ldc_I4, 3546)]).ThrowIfNotMatch("Failed to find '3546'");
        matcher.Opcode = OpCodes.Ldarg_3;
        matcher.Operand = null;
        return matcher.Instructions();
    }

    private static void MakeGridLikeSaleButtons(ShopMenu shopMenu)
    {
        int perRowV = shopMenu.itemPriceAndStock.Values.Any(stockInfo =>
            stockInfo.Price > 0 || stockInfo.TradeItemCount > 0
        )
            ? ModEntry.Config.ShopItemPerRow
            : ModEntry.Config.DresserItemPerRow;
        perRowV = Math.Clamp(perRowV, 1, 9);
        SetPerRow(perRowV, shopMenu.forSale.Count);

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
                            idx < perRowV * (ShopMenu.itemsPerPage - 1)
                                ? myID + perRowV
                                : ClickableComponent.CUSTOM_SNAP_BEHAVIOR,
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
        Label lbl = (Label)matcher.Operand;
        matcher.Advance(1).Insert([new(OpCodes.Br, lbl)]);

        return matcher.Instructions();
    }

    public static void DrawShadowOrBoldText(
        SpriteBatch b,
        string s,
        Vector2 position,
        Color? color,
        float alpha,
        float layerDepth,
        SpriteFont? spriteFont = null
    )
    {
        if (color == null)
        {
            Utility.drawTextWithShadow(
                b,
                s,
                spriteFont ?? Game1.dialogueFont,
                position,
                Game1.textColor * alpha,
                layerDepth: layerDepth
            );
        }
        else
        {
            Color txtColor = (Color)color * alpha;
            Utility.drawTextWithColoredShadow(
                b,
                s,
                spriteFont ?? Game1.dialogueFont,
                position,
                txtColor,
                txtColor * 0.5f,
                layerDepth: layerDepth
            );
        }
    }

    public static void DrawDisplayName(
        SpriteBatch b,
        string s,
        int x,
        int y,
        int characterPosition,
        int width,
        int height,
        float alpha,
        float layerDepth,
        bool junimoText,
        int drawBGScroll,
        string placeHolderScrollWidthText,
        Color? color,
        SpriteText.ScrollTextAlignment scroll_text_alignment,
        ISalable salable
    )
    {
        if (perRow.Value <= 1)
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
            return;
        }
        if (!salable.ShouldDrawIcon())
        {
            Vector2 stringSize = Game1.dialogueFont.MeasureString(s);
            x -= 24;
            y += (int)(stringSize.Y * 2 / 3);
            DrawShadowOrBoldText(b, s, new Vector2(x, y), color, alpha, layerDepth, Game1.smallFont);
            x += (int)stringSize.X;
        }
        if (salable.Stack > 1)
        {
            DrawShadowOrBoldText(b, "x" + salable.Stack, new Vector2(x, y), color, alpha, layerDepth);
        }
    }

    public static void DrawPrice(
        SpriteBatch b,
        string s,
        int x,
        int y,
        int characterPosition,
        int width,
        int height,
        float alpha,
        float layerDepth,
        bool junimoText,
        int drawBGScroll,
        string placeHolderScrollWidthText,
        Color? color,
        SpriteText.ScrollTextAlignment scroll_text_alignment,
        ClickableComponent component
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
        else
        {
            Vector2 stringSize = Game1.dialogueFont.MeasureString(s);
            Vector2 position = new(component.bounds.Right - stringSize.X - 32, component.bounds.Top + 12);
            DrawShadowOrBoldText(b, s, position, color, alpha, layerDepth);
        }
    }

    public static void DrawPriceIcon(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle sourceRect,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        bool flipped,
        float layerDepth,
        int horizontalShadowOffset,
        int verticalShadowOffset,
        float shadowIntensity,
        ClickableComponent component
    )
    {
        if (perRow.Value <= 2)
        {
            Utility.drawWithShadow(
                b,
                texture,
                position,
                sourceRect,
                color,
                rotation,
                origin,
                scale,
                flipped,
                layerDepth,
                horizontalShadowOffset,
                verticalShadowOffset,
                shadowIntensity
            );
        }
        else
        {
            Utility.drawWithShadow(
                b,
                texture,
                new Vector2(component.bounds.Right - 36, component.bounds.Top + 24),
                sourceRect,
                color,
                rotation,
                origin,
                2f,
                flipped,
                layerDepth,
                horizontalShadowOffset,
                verticalShadowOffset,
                shadowIntensity
            );
        }
    }

    public static void DrawTradeCount(
        SpriteBatch b,
        string s,
        int x,
        int y,
        int characterPosition,
        int width,
        int height,
        float alpha,
        float layerDepth,
        bool junimoText,
        int drawBGScroll,
        string placeHolderScrollWidthText,
        Color? color,
        SpriteText.ScrollTextAlignment scroll_text_alignment,
        ClickableComponent component
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
        else
        {
            Vector2 stringSize = Game1.dialogueFont.MeasureString(s);
            Vector2 position =
                new(component.bounds.Right - stringSize.X - 24, component.bounds.Bottom - stringSize.Y - 8);
            DrawShadowOrBoldText(b, s, position, color, alpha, layerDepth);
        }
    }

    public static void DrawTradeIcon(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle sourceRect,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        bool flipped,
        float layerDepth,
        int horizontalShadowOffset,
        int verticalShadowOffset,
        float shadowIntensity,
        ClickableComponent component,
        int count
    )
    {
        if (perRow.Value <= 2)
        {
            Utility.drawWithShadow(
                b,
                texture,
                position,
                sourceRect,
                color,
                rotation,
                origin,
                scale,
                flipped,
                layerDepth,
                horizontalShadowOffset,
                verticalShadowOffset,
                shadowIntensity
            );
        }
        else
        {
            Vector2 stringSize = Game1.dialogueFont.MeasureString("x" + count);
            Utility.drawWithShadow(
                b,
                texture,
                new Vector2(
                    component.bounds.Right - stringSize.X - 24 - sourceRect.Width * 2,
                    component.bounds.Bottom - sourceRect.Height * 2 - 16
                ),
                sourceRect,
                color,
                rotation,
                origin,
                2f,
                flipped,
                layerDepth,
                horizontalShadowOffset,
                verticalShadowOffset,
                shadowIntensity
            );
        }
    }

    private static IEnumerable<CodeInstruction> ShopMenu_draw_Transpiler_strong(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        CodeMatcher matcher = new(instructions, generator);

        // IL_01b2: ldloc.s 4
        // IL_01b4: ldflda valuetype[MonoGame.Framework]Microsoft.Xna.Framework.Rectangle StardewValley.Menus.ClickableComponent::bounds
        // IL_01b9: ldfld int32[MonoGame.Framework]Microsoft.Xna.Framework.Rectangle::X
        matcher.MatchStartForward(
            [
                new(inst => inst.IsLdloc()),
                new(
                    OpCodes.Ldflda,
                    AccessTools.DeclaredField(typeof(ClickableComponent), nameof(ClickableComponent.bounds))
                ),
                new(OpCodes.Ldfld, AccessTools.DeclaredField(typeof(Rectangle), nameof(Rectangle.X))),
            ]
        );
        CodeInstruction ldlocClickableComponent = matcher.Instruction.Clone();

        // IL_0242: ldloc.s 6
        // IL_0244: callvirt instance bool StardewValley.ISalable::ShouldDrawIcon()
        // IL_0249: brfalse IL_04e7
        matcher.MatchStartForward(
            [
                new(inst => inst.IsLdloc()),
                new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(ISalable), nameof(ISalable.ShouldDrawIcon))),
                new(OpCodes.Brfalse),
            ]
        );
        CodeInstruction ldlocSalable = matcher.Instruction.Clone();

        // display name text
        matcher
            .MatchStartForward(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
            )
            .InsertAndAdvance([ldlocSalable.Clone()]);
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawDisplayName));
        matcher
            .MatchStartForward(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
            )
            .InsertAndAdvance([ldlocSalable.Clone()]);
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawDisplayName));

        // price text + icon
        matcher
            .MatchStartForward(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
            )
            .InsertAndAdvance([ldlocClickableComponent.Clone()]);
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawPrice));
        matcher.MatchStartForward(
            [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.drawWithShadow)))]
        );
        matcher.Opcode = ldlocClickableComponent.opcode;
        matcher.Operand = ldlocClickableComponent.operand;
        matcher
            .Advance(1)
            .InsertAndAdvance([new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawPriceIcon)))]);

        // trade item text + icon
        matcher.MatchStartForward(
            [
                new(inst => inst.IsLdloc()),
                new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(ShopMenu), nameof(ShopMenu.HasTradeItem))),
            ]
        );
        CodeInstruction ldlocCount = matcher.Instruction.Clone();
        matcher.MatchStartForward(
            [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Utility), nameof(Utility.drawWithShadow)))]
        );
        matcher.Opcode = ldlocClickableComponent.opcode;
        matcher.Operand = ldlocClickableComponent.operand;
        matcher
            .Advance(1)
            .InsertAndAdvance(
                [
                    ldlocCount.Clone(),
                    new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawTradeIcon))),
                ]
            );
        matcher
            .MatchStartForward(
                [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
            )
            .InsertAndAdvance([ldlocClickableComponent.Clone()]);
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawTradeCount));

        return matcher.Instructions();
    }

    private static IEnumerable<CodeInstruction> ShopMenu_draw_Transpiler_weak(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        CodeMatcher matcher = new(instructions, generator);

        // display name text
        matcher.MatchStartForward(
            [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
        );
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawDisplayName));
        matcher.MatchStartForward(
            [new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(SpriteText), nameof(SpriteText.drawString)))]
        );
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(DrawDisplayName));

        return matcher.Instructions();
    }

    private static int ShopMenu_StackCount() => ModEntry.Config.StackCount;

    private static IEnumerable<CodeInstruction> ShopMenu_replaceStackCounts_transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        CodeMatcher matcher = new(instructions, generator);

        matcher.MatchStartForward([new(OpCodes.Ldc_I4_S, (sbyte)25), new(OpCodes.Br_S), new(OpCodes.Ldc_I4, 999)]);
        matcher.Opcode = OpCodes.Call;
        matcher.Operand = AccessTools.DeclaredMethod(typeof(Patches), nameof(ShopMenu_StackCount));

        return matcher.Instructions();
    }
}
