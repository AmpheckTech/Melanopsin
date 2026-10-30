using System.Globalization;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement("Input")]
public sealed class TextInput :
    Control,
    ITextContentControl,
    IInputHandler,
    IKeyboardInputHandler,
    ICompositionInputHandler,
    IRenderCommandProvider,
    ICustomTextRendering {
    private readonly TextEditingBuffer _editing = new();
    private bool _dragging;
    private Rect _textBounds;
    private ITextLayout? _lastTextLayout;
    private FontFace? _lastFont;
    private float _lastFontSize;
    private float _scrollX;
    private Color _selectionColor = new(118, 169, 154, 120);
    private Color _caretColor = new(26, 87, 72);

    public TextInput()
        : base("Input") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.TextInput;
    }

    public event Action<string>? TextChanged;

    public event Action<string>? Submitted;

    [MarkupAttribute("text")]
    public string Text {
        get => _editing.Text;
        set {
            if (!_editing.SetText(value ?? string.Empty)) {
                return;
            }

            SynchronizeAccessibility();
            InvalidateLayout();
        }
    }

    [MarkupAttribute("placeholder")]
    public string? Placeholder { get; private set; }

    [MarkupAttribute("password", MarkupValueKind.Boolean)]
    public bool IsPassword { get; private set; }

    [MarkupAttribute("read-only", MarkupValueKind.Boolean)]
    public bool IsReadOnly { get; private set; }

    [MarkupAttribute("max-length", MarkupValueKind.Integer)]
    public int MaxLength => _editing.MaxLength;

    public int CaretIndex => _editing.CaretIndex;

    public int SelectionStart => _editing.SelectionStart;

    public int SelectionLength => _editing.SelectionLength;

    public string CompositionText => _editing.CompositionText;

    public Color SelectionColor {
        get => _selectionColor;
        set {
            if (_selectionColor == value) {
                return;
            }

            _selectionColor = value;
            InvalidatePaint();
        }
    }

    public Color CaretColor {
        get => _caretColor;
        set {
            if (_caretColor == value) {
                return;
            }

            _caretColor = value;
            InvalidatePaint();
        }
    }

    public void AppendText(string value) {
        SetText(Text + value);
    }

    public void SetText(string value, bool moveCaretToEnd = true) {
        if (!_editing.SetText(value ?? string.Empty)) {
            return;
        }

        if (moveCaretToEnd) {
            _editing.MoveCaret(Text.Length, extendSelection: false);
        } else {
            _editing.CollapseSelection();
        }

        SynchronizeAccessibility();
        InvalidateLayout();
        TextChanged?.Invoke(Text);
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down || IsReadOnly || !IsEnabled) {
            return false;
        }

        if (input.Control &&
            (input.Key == Key.A ||
             input.Key == Key.Unknown &&
             input.Text?.Equals("a", StringComparison.OrdinalIgnoreCase) == true)) {
            SelectAll();
            return true;
        }

        switch (input.Key) {
            case Key.Backspace:
                if (_editing.DeleteBackward()) {
                    OnTextEdited();
                }

                return true;
            case Key.Delete:
                if (_editing.DeleteForward()) {
                    OnTextEdited();
                }

                return true;
            case Key.Left:
                MoveCaret(
                    _editing.PreviousTextElementStart(_editing.CaretIndex),
                    input.Shift);
                return true;
            case Key.Right:
                MoveCaret(
                    _editing.NextTextElementStart(_editing.CaretIndex),
                    input.Shift);
                return true;
            case Key.Home:
                MoveCaret(0, input.Shift);
                return true;
            case Key.End:
                MoveCaret(Text.Length, input.Shift);
                return true;
            case Key.Enter:
                Submitted?.Invoke(Text);
                return true;
        }

        var insertableText = FilterControlCharacters(input.Text);
        if (insertableText.Length == 0) {
            return false;
        }

        ReplaceSelection(insertableText);
        return true;
    }

    public bool HandleComposition(TextCompositionEvent input) {
        if (IsReadOnly || !IsEnabled) {
            return false;
        }

        switch (input.Phase) {
            case TextCompositionPhase.Start:
                _editing.BeginComposition(input.Text);
                break;
            case TextCompositionPhase.Update:
                _editing.UpdateComposition(input.Text);
                break;
            case TextCompositionPhase.Commit:
                _editing.CommitComposition(input.Text);
                OnTextEdited();
                return true;
            case TextCompositionPhase.Cancel:
                _editing.CancelComposition();
                break;
            default:
                return false;
        }

        SynchronizeAccessibility();
        InvalidateLayout();
        return true;
    }

    public void Handle(RoutedInputEvent inputEvent) {
        var phase = inputEvent.Input.Phase;
        if (phase is PointerPhase.Up or PointerPhase.Cancel) {
            var wasDragging = _dragging;
            _dragging = false;
            inputEvent.Handled = wasDragging;
            return;
        }

        if (IsReadOnly || !IsEnabled) {
            _dragging = false;
            return;
        }

        if (phase == PointerPhase.Down) {
            _dragging = true;
            _editing.MoveCaret(
                CaretFromX(inputEvent.LayoutX),
                extendSelection: false);
            InvalidatePaint();
            inputEvent.Handled = true;
        } else if (phase == PointerPhase.Move && _dragging) {
            _editing.MoveCaret(
                CaretFromX(inputEvent.LayoutX),
                extendSelection: true);
            InvalidatePaint();
            inputEvent.Handled = true;
        }
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        _textBounds = new Rect(
            context.Bounds.X + context.Padding,
            context.Bounds.Y,
            Math.Max(0, context.Bounds.Width - (context.Padding * 2)),
            context.Bounds.Height);
        if (context.TextLayout is { } textLayout) {
            AppendShapedTextCommands(textLayout, context, commands);
            return;
        }

        var sourceText = _editing.HasComposition
            ? _editing.TextWithComposition
            : Text;
        var sourceCaret = _editing.HasComposition
            ? _editing.CompositionStart + CompositionText.Length
            : CaretIndex;
        var sourceSelectionStart = _editing.HasComposition
            ? sourceCaret
            : SelectionStart;
        var sourceSelectionLength = _editing.HasComposition
            ? 0
            : SelectionLength;
        commands.Add(new DrawEditableTextCommand(
            sourceText,
            Placeholder,
            _textBounds,
            context.Foreground,
            new Color(130, 145, 138),
            SelectionColor,
            CaretColor,
            context.FontSize,
            sourceSelectionStart,
            sourceSelectionLength,
            sourceCaret,
            IsPassword,
            IsFocused && !IsReadOnly,
            context.Font));
    }

    private void AppendShapedTextCommands(
        ITextLayout textLayout,
        RenderCommandContext context,
        List<RenderCommand> commands) {
        var sourceText = _editing.HasComposition
            ? _editing.TextWithComposition
            : Text;
        var graphemes = StringInfo.ParseCombiningCharacters(sourceText);
        var displayText = IsPassword
            ? new string('•', graphemes.Length)
            : sourceText;
        var sourceCaret = _editing.HasComposition
            ? _editing.CompositionStart + CompositionText.Length
            : CaretIndex;
        var displayCaret = MapPasswordIndex(
            graphemes,
            sourceCaret,
            sourceText.Length);
        var displaySelectionStart = _editing.HasComposition
            ? displayCaret
            : MapPasswordIndex(graphemes, SelectionStart, sourceText.Length);
        var displaySelectionEnd = !_editing.HasComposition
            ? MapPasswordIndex(
                graphemes,
                SelectionStart + SelectionLength,
                sourceText.Length)
            : displayCaret;
        var shaped = textLayout.Layout(
            displayText,
            new TextLayoutOptions(
                context.FontSize,
                maxWidth: float.PositiveInfinity,
                font: context.Font));
        var caretX = MeasurePrefixWidth(
            textLayout,
            displayText,
            displayCaret,
            context.FontSize,
            context.Font);
        var selectionStartX = MeasurePrefixWidth(
            textLayout,
            displayText,
            displaySelectionStart,
            context.FontSize,
            context.Font);
        var selectionEndX = MeasurePrefixWidth(
            textLayout,
            displayText,
            displaySelectionEnd,
            context.FontSize,
            context.Font);
        var scrollX = Math.Max(0, caretX - _textBounds.Width);
        _lastTextLayout = textLayout;
        _lastFont = context.Font;
        _lastFontSize = context.FontSize;
        _scrollX = scrollX;
        var placeholder = Text.Length == 0 && !string.IsNullOrEmpty(Placeholder)
            ? textLayout.Layout(
                Placeholder,
                new TextLayoutOptions(
                    context.FontSize,
                    maxWidth: float.PositiveInfinity,
                    font: context.Font))
            : null;
        commands.Add(new DrawShapedEditableTextCommand(
            _textBounds,
            shaped,
            placeholder,
            context.Foreground,
            new Color(130, 145, 138),
            SelectionColor,
            CaretColor,
            caretX,
            selectionStartX,
            selectionEndX,
            scrollX,
            IsFocused && !IsReadOnly));
    }

    private static float MeasurePrefixWidth(
        ITextLayout textLayout,
        string text,
        int length,
        float fontSize,
        FontFace? font) {
        if (length <= 0 || text.Length == 0) {
            return 0;
        }

        return textLayout.Layout(
            text[..Math.Min(length, text.Length)],
            new TextLayoutOptions(
                fontSize,
                maxWidth: float.PositiveInfinity,
                font: font))
            .Width;
    }

    private int MapPasswordIndex(
        IReadOnlyList<int> graphemeStarts,
        int utf16Index,
        int sourceLength) {
        if (!IsPassword || sourceLength == 0) {
            return Math.Clamp(utf16Index, 0, sourceLength);
        }

        var count = 0;
        foreach (var start in graphemeStarts) {
            if (start >= utf16Index) {
                break;
            }

            count++;
        }

        return count;
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value?.ToString() ?? string.Empty;
                break;
            case "placeholder":
                Placeholder = value?.ToString();
                break;
            case "password":
                var isPassword = ControlValueParser.ReadBoolean(value) ?? false;
                if (IsPassword == isPassword) {
                    break;
                }

                IsPassword = isPassword;
                SynchronizeAccessibility();
                InvalidatePaint();
                break;
            case "read-only":
                IsReadOnly = ControlValueParser.ReadBoolean(value) ?? false;
                SetState(UiStateNames.ReadOnly, IsReadOnly);
                break;
            case "max-length":
                if (_editing.SetMaxLength(
                        ControlValueParser.ReadInteger(value) ?? MaxLength)) {
                    SynchronizeAccessibility();
                    InvalidateLayout();
                }

                break;
        }
    }

    private void SelectAll() {
        _editing.SelectAll();
        InvalidatePaint();
    }

    private void MoveCaret(int index, bool extendSelection) {
        _editing.MoveCaret(index, extendSelection);
        InvalidatePaint();
    }

    private void ReplaceSelection(string value) {
        _editing.ReplaceSelection(value);
        OnTextEdited();
    }

    private void OnTextEdited() {
        SynchronizeAccessibility();
        InvalidateLayout();
        TextChanged?.Invoke(Text);
    }

    private void SynchronizeAccessibility() {
        if (!IsPassword) {
            Accessibility.Name = Text;
            Accessibility.Value = Text;
            return;
        }

        Accessibility.Name = "Password";
        Accessibility.Value = new string(
            '•',
            StringInfo.ParseCombiningCharacters(Text).Length);
    }

    private int CaretFromX(float x) {
        if (_textBounds.Width <= 0) {
            return 0;
        }

        if (_lastTextLayout is { } textLayout) {
            var displayText = IsPassword && Text.Length > 0
                ? new string('•', StringInfo.ParseCombiningCharacters(Text).Length)
                : Text;
            var hit = textLayout.HitTestCaret(
                displayText,
                Math.Clamp(
                    x - _textBounds.X + _scrollX,
                    0,
                    _textBounds.Width + _scrollX),
                new TextLayoutOptions(
                    _lastFontSize,
                    maxWidth: float.PositiveInfinity,
                    font: _lastFont));
            return MapDisplayIndexToOriginal(hit.TextOffset);
        }

        var ratio = Math.Clamp((x - _textBounds.X) / _textBounds.Width, 0, 1);
        var starts = StringInfo.ParseCombiningCharacters(Text);
        if (starts.Length == 0) {
            return 0;
        }

        var target = ratio * starts.Length;
        var element = (int)Math.Round(target, MidpointRounding.AwayFromZero);
        return element >= starts.Length ? Text.Length : starts[element];
    }

    private static string FilterControlCharacters(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        var firstControl = -1;
        for (var index = 0; index < value.Length; index++) {
            if (char.IsControl(value[index])) {
                firstControl = index;
                break;
            }
        }

        if (firstControl < 0) {
            return value;
        }

        var filtered = new System.Text.StringBuilder(value.Length - 1);
        filtered.Append(value, 0, firstControl);
        for (var index = firstControl + 1; index < value.Length; index++) {
            if (!char.IsControl(value[index])) {
                filtered.Append(value[index]);
            }
        }

        return filtered.ToString();
    }

    private int MapDisplayIndexToOriginal(int displayIndex) {
        if (!IsPassword || Text.Length == 0) {
            return Math.Clamp(displayIndex, 0, Text.Length);
        }

        var starts = StringInfo.ParseCombiningCharacters(Text);
        return displayIndex >= starts.Length
            ? Text.Length
            : starts[Math.Max(0, displayIndex)];
    }
}
