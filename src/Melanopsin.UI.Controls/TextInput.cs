using System.Globalization;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement("Input")]
public sealed partial class TextInput :
    Control,
    ITextContentControl,
    IInputHandler,
    IKeyboardInputHandler,
    ICompositionInputHandler,
    IRenderCommandProvider,
    ICustomTextRendering {
    private readonly TextEditingBuffer _editing = new();
    private bool _dragging;
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
        var input = inputEvent.Input;
        if (!input.IsPrimaryButton) {
            return;
        }

        var phase = input.Phase;
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

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        switch (name) {
            case "text":
                Text = value as string ?? string.Empty;
                break;
            case "placeholder":
                Placeholder = value as string;
                break;
            case "password":
                var isPassword = value is bool password && password;
                if (IsPassword == isPassword) {
                    break;
                }

                IsPassword = isPassword;
                SynchronizeAccessibility();
                InvalidatePaint();
                break;
            case "read-only":
                IsReadOnly = value is bool readOnly && readOnly;
                SetState(UiStateNames.ReadOnly, IsReadOnly);
                break;
            case "max-length":
                if (_editing.SetMaxLength(
                        value is int maxLength ? maxLength : MaxLength)) {
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

}
