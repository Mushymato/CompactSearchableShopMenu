/*
  █████    █████                              █████
 ░░███    ░░███                              ░░███
 ███████   ░███████   █████ █████     ██████  ░███ █████ █████ ████
░░░███░    ░███░░███ ░░███ ░░███     ███░░███ ░███░░███ ░░███ ░███
  ░███     ░███ ░███  ░░░█████░     ░███████  ░██████░   ░███ ░███
  ░███ ███ ░███ ░███   ███░░░███    ░███░░░   ░███░░███  ░███ ░███
  ░░█████  ████ █████ █████ █████   ░░██████  ████ █████ ░░███████
   ░░░░░  ░░░░ ░░░░░ ░░░░░ ░░░░░     ░░░░░░  ░░░░ ░░░░░   ░░░░░███
                                                          ███ ░███
                                                         ░░██████
                                                          ░░░░░░
*/

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace CompactSearchableShopMenu;

internal sealed class CenteredTextBox(Texture2D textBoxTexture, SpriteFont font, Color textColor)
    : TextBox(textBoxTexture, null, font, textColor)
{
    // The display stays unselected, so it draws its own caret.
    public bool ShowCaret;

    public override void Draw(SpriteBatch spriteBatch, bool drawShadow = true)
    {
        int texH = _textBoxTexture.Height;
        int capW = (int)Math.Round(16f * Height / texH);
        spriteBatch.Draw(
            _textBoxTexture,
            new Rectangle(X, Y, capW, Height),
            new Rectangle(0, 0, 16, texH),
            Color.White
        );
        spriteBatch.Draw(
            _textBoxTexture,
            new Rectangle(X + capW, Y, Width - 2 * capW, Height),
            new Rectangle(16, 0, 4, texH),
            Color.White
        );
        spriteBatch.Draw(
            _textBoxTexture,
            new Rectangle(X + Width - capW, Y, capW, Height),
            new Rectangle(_textBoxTexture.Bounds.Width - 16, 0, 16, texH),
            Color.White
        );

        Vector2 size = _font.MeasureString(Text);
        var position = new Vector2(X + (Width - size.X) / 2f, Y + (Height - size.Y) / 2f);
        if (drawShadow)
            Utility.drawTextWithShadow(spriteBatch, Text, _font, position, _textColor);
        else
            spriteBatch.DrawString(_font, Text, position, _textColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0.99f);

        if (ShowCaret && Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000.0 >= 500.0)
            spriteBatch.Draw(
                Game1.staminaRect,
                new Rectangle((int)(position.X + size.X) + 2, Y + (Height - 32) / 2, 4, 32),
                _textColor
            );
    }
}

public readonly struct NumberPadRects(Rectangle preview, Rectangle[] digits, Rectangle clear, Rectangle ok)
{
    public readonly Rectangle Preview = preview;
    public readonly Rectangle Clear = clear;
    public readonly Rectangle Ok = ok;
    private readonly Rectangle[] _digits = digits;

    public Rectangle Digit(int value) => _digits[value];
}

public static class NumberPadLayout
{
    public static NumberPadRects Compute(int x, int y, int w, int h, int previewH, int gap, int margin)
    {
        var preview = new Rectangle(x + margin, y + margin, w - 2 * margin, previewH);
        int gridTop = y + margin + previewH + gap;

        int keyFromWidth = (w - 2 * margin - 2 * gap) / 3;
        int keyFromHeight = (h - 2 * margin - previewH - 4 * gap) / 4;
        int key = Math.Min(keyFromWidth, keyFromHeight);

        int gridW = 3 * key + 2 * gap;
        int gridX = x + (w - gridW) / 2;

        Rectangle Cap(int row, int col) =>
            new Rectangle(gridX + col * (key + gap), gridTop + row * (key + gap), key, key);

        var digits = new Rectangle[10];
        digits[0] = Cap(3, 1);
        for (int d = 1; d <= 9; d++)
            digits[d] = Cap((9 - d) / 3, (d - 1) % 3);

        return new NumberPadRects(preview, digits, Cap(3, 0), Cap(3, 2));
    }
}

internal sealed class NumberPadState(int maxLength = 4)
{
    private readonly int _maxLength = maxLength;

    public string Pending { get; private set; } = string.Empty;

    public void AppendDigit(char c)
    {
        if (c < '0' || c > '9')
            return;
        if (Pending.Length >= _maxLength)
            return;
        Pending += c;
    }

    public void Clear() => Pending = string.Empty;

    public void Preset(int quantity)
    {
        string s = (quantity < 0 ? 0 : quantity).ToString();
        Pending = s.Length > _maxLength ? s.Substring(0, _maxLength) : s;
    }

    public int CommitValue(int maxBuyable)
    {
        if (maxBuyable <= 0)
            return 0;
        string digits = new(Pending.Where(char.IsDigit).ToArray());
        int requested = int.TryParse(digits, out int n) ? n : 0;
        return Math.Clamp(requested, 1, maxBuyable);
    }
}

internal sealed class NumberPadMenu : IClickableMenu
{
    private const int KeySize = 64;
    private const int Gap = 16;
    private const int Margin = 32;
    private const int PreviewHeight = 64;
    private const int PanelWidth = 2 * Margin + 3 * KeySize + 2 * Gap;
    private const int PanelHeight = 2 * Margin + PreviewHeight + 4 * Gap + 4 * KeySize;

    private const int IdDigitBase = 5200;
    private const int IdClear = 5210;
    private const int IdOk = 5211;

    private static readonly Rectangle BlankCapArt = new Rectangle(33, 177, 14, 14);
    private const float BlankCapScale = KeySize / 14f;

    private readonly NumberPadState _state = new();
    private readonly int _maxBuyable;
    private readonly Action<int> _onCommit;
    private readonly Action<IClickableMenu>? _onClosedRestoreFocus;
    private readonly Action _onClosed;

    private readonly bool _snappyAtOpen;

    private readonly CenteredTextBox _preview;
    private readonly ClickableTextureComponent[] _digitKeys = new ClickableTextureComponent[10];
    private readonly ClickableTextureComponent _clearKey;
    private readonly ClickableTextureComponent _okKey;

    private bool _closed;

    public NumberPadMenu(
        int currentQuantity,
        int maxBuyable,
        Action<int> onCommit,
        Action<IClickableMenu>? onClosedRestoreFocus,
        Action onClosed
    )
        : base(
            (int)Utility.getTopLeftPositionForCenteringOnScreen(PanelWidth, PanelHeight).X,
            (int)Utility.getTopLeftPositionForCenteringOnScreen(PanelWidth, PanelHeight).Y,
            PanelWidth,
            PanelHeight
        )
    {
        _maxBuyable = maxBuyable;
        _onCommit = onCommit;
        _onClosedRestoreFocus = onClosedRestoreFocus;
        _onClosed = onClosed;
        _state.Preset(currentQuantity);
        _snappyAtOpen = Game1.options.snappyMenus && Game1.options.gamepadControls;

        _preview = new CenteredTextBox(
            Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
            Game1.smallFont,
            Game1.textColor
        )
        {
            numbersOnly = true,
            textLimit = 4,
            ShowCaret = true,
        };

        for (int d = 0; d <= 9; d++)
        {
            _digitKeys[d] = new ClickableTextureComponent(
                new Rectangle(0, 0, KeySize, KeySize),
                Game1.mouseCursors2,
                BlankCapArt,
                BlankCapScale
            )
            {
                name = d.ToString(),
                myID = IdDigitBase + d,
            };
        }
        _clearKey = new ClickableTextureComponent(
            new Rectangle(0, 0, KeySize, KeySize),
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47),
            1f
        )
        {
            name = "Clear",
            myID = IdClear,
        };
        _okKey = new ClickableTextureComponent(
            new Rectangle(0, 0, KeySize, KeySize),
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46),
            1f
        )
        {
            name = "OK",
            myID = IdOk,
        };

        WireNeighbors();
        RepositionElements();
        populateClickableComponentList();
        if (Game1.options.SnappyMenus)
            snapToDefaultClickableComponent();
        Game1.playSound("bigSelect");
    }

    private void WireNeighbors()
    {
        ClickableTextureComponent[][] grid =
        [
            new[] { _digitKeys[7], _digitKeys[8], _digitKeys[9] },
            [_digitKeys[4], _digitKeys[5], _digitKeys[6]],
            [_digitKeys[1], _digitKeys[2], _digitKeys[3]],
            [_clearKey, _digitKeys[0], _okKey],
        ];
        for (int row = 0; row < grid.Length; row++)
        {
            for (int col = 0; col < grid[row].Length; col++)
            {
                var key = grid[row][col];
                if (row > 0)
                    key.upNeighborID = grid[row - 1][col].myID;
                if (row < grid.Length - 1)
                    key.downNeighborID = grid[row + 1][col].myID;
                if (col > 0)
                    key.leftNeighborID = grid[row][col - 1].myID;
                if (col < grid[row].Length - 1)
                    key.rightNeighborID = grid[row][col + 1].myID;
            }
        }
    }

    private void RepositionElements()
    {
        xPositionOnScreen = (int)Utility.getTopLeftPositionForCenteringOnScreen(width, height).X;
        yPositionOnScreen = (int)Utility.getTopLeftPositionForCenteringOnScreen(width, height).Y;

        var rects = NumberPadLayout.Compute(
            xPositionOnScreen,
            yPositionOnScreen,
            width,
            height,
            PreviewHeight,
            Gap,
            Margin
        );
        for (int d = 0; d <= 9; d++)
            _digitKeys[d].bounds = rects.Digit(d);
        _clearKey.bounds = rects.Clear;
        _okKey.bounds = rects.Ok;

        _preview.X = rects.Preview.X;
        _preview.Y = rects.Preview.Y;
        _preview.Width = rects.Preview.Width;
        _preview.Height = rects.Preview.Height;
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents = new List<ClickableComponent>();
        foreach (var key in _digitKeys)
            allClickableComponents.Add(key);
        allClickableComponents.Add(_clearKey);
        allClickableComponents.Add(_okKey);
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = getComponentWithID(IdOk);
        snapCursorToCurrentSnappedComponent();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RepositionElements();
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        foreach (var key in _digitKeys)
            key.tryHover(x, y);
        _clearKey.tryHover(x, y);
        _okKey.tryHover(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        for (int d = 0; d <= 9; d++)
        {
            if (_digitKeys[d].containsPoint(x, y))
            {
                PressDigit((char)('0' + d));
                return;
            }
        }
        if (_clearKey.containsPoint(x, y))
        {
            PressClear();
            return;
        }
        if (_okKey.containsPoint(x, y))
        {
            Commit();
            return;
        }
        if (!isWithinBounds(x, y))
            Cancel();
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            Cancel();
            return;
        }
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            PressDigit((char)('0' + (key - Keys.D0)));
            return;
        }
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            PressDigit((char)('0' + (key - Keys.NumPad0)));
            return;
        }
        if (key == Keys.Back)
        {
            Backspace();
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        switch (button)
        {
            case Buttons.B:
                Cancel();
                break;
            case Buttons.Start:
                Commit();
                break;
            default:
                base.receiveGamePadButton(button);
                break;
        }
    }

    private void PressDigit(char digit)
    {
        int before = _state.Pending.Length;
        _state.AppendDigit(digit);
        if (_state.Pending.Length != before)
            Game1.playSound("cowboy_monsterhit");
    }

    private void PressClear()
    {
        if (_state.Pending.Length == 0)
            return;
        _state.Clear();
        Game1.playSound("tinyWhip");
    }

    private void Backspace()
    {
        string pending = _state.Pending;
        if (pending.Length == 0)
            return;
        _state.Clear();
        for (int i = 0; i < pending.Length - 1; i++)
            _state.AppendDigit(pending[i]);
        Game1.playSound("tinyWhip");
    }

    private void Commit()
    {
        if (_closed)
            return;
        _closed = true;
        _onCommit?.Invoke(_state.CommitValue(_maxBuyable));
        CloseAndRestoreFocus();
    }

    private void Cancel()
    {
        if (_closed)
            return;
        _closed = true;
        CloseAndRestoreFocus();
    }

    private void CloseAndRestoreFocus()
    {
        IClickableMenu parent = GetParentMenu();
        exitThisMenu();
        if (_snappyAtOpen && parent != null)
            _onClosedRestoreFocus?.Invoke(parent);
        _onClosed?.Invoke();
    }

    public override void draw(SpriteBatch b)
    {
        if (!Game1.options.showClearBackgrounds)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);
        }

        drawTextureBox(
            b,
            Game1.menuTexture,
            new Rectangle(0, 256, 60, 60),
            xPositionOnScreen,
            yPositionOnScreen,
            width,
            height,
            Color.White
        );

        foreach (var key in _digitKeys)
        {
            key.draw(b);
            DrawKeyLabel(b, key);
        }
        _clearKey.draw(b);
        _okKey.draw(b);

        _preview.Text = $"{_state.Pending} / {_maxBuyable}";
        _preview.Draw(b);

        base.draw(b);
        drawMouse(b, ignore_transparency: true);
    }

    private static void DrawKeyLabel(SpriteBatch b, ClickableTextureComponent key)
    {
        Vector2 size = Game1.dialogueFont.MeasureString(key.name);
        Vector2 position = Utility.snapDrawPosition(
            new Vector2(key.bounds.Center.X - size.X / 2f, key.bounds.Center.Y - size.Y / 2f)
        );
        b.DrawString(Game1.dialogueFont, key.name, position, Color.Black);
    }
}
