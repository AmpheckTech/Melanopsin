using System.Globalization;
using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Input;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement("Input")]
public sealed partial class TextInput :
    Control,
    ISingleLineTextContent,
    IInputHandler,
    IKeyboardInputHandler,
    ICompositionInputHandler,
    ITextInputDeleteHandler,
    ITextInputSource,
    IRenderCommandProvider,
    ICustomTextRendering {
    private const long MultiClickIntervalMilliseconds = 500;
    private const float MultiClickDistance = 5;
    private static long _nextEditorId;
    private readonly TextEditingBuffer _editing = new();
    private readonly long _editorId = Interlocked.Increment(ref _nextEditorId);
    private bool _dragging;
    private int _clickCount;
    private long _lastClickTimestamp;
    private float _lastClickX;
    private float _lastClickY;
    private Color _selectionColor = new(118, 169, 154, 120);
    private Color _caretColor = new(26, 87, 72);
    private TextInputPurpose _purpose;

    public TextInput()
        : base("Input") {
        IsRepaintBoundary = true;
        InputHandler = this;
        IsFocusable = true;
        Accessibility.Role = AccessibilityRole.TextInput;
        SynchronizeAccessibility();
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

            _textRevision++;
            SynchronizeAccessibility();
            InvalidateLayout();
        }
    }

    [MarkupAttribute("placeholder")]
    public string? Placeholder {
        get;
        private set {
            if (string.Equals(field, value, StringComparison.Ordinal)) {
                return;
            }

            field = value;
            SynchronizeAccessibility();
            InvalidatePaint();
        }
    }

    [MarkupAttribute("password", MarkupValueKind.Boolean)]
    public bool IsPassword { get; private set; }

    [MarkupAttribute("read-only", MarkupValueKind.Boolean)]
    public bool IsReadOnly { get; private set; }

    [MarkupAttribute("max-length", MarkupValueKind.Integer)]
    public int MaxLength => _editing.MaxLength;

    public int CaretIndex => _editing.CaretIndex;

    public long EditorId => _editorId;

    public int SelectionStart => _editing.SelectionStart;

    public int SelectionLength => _editing.SelectionLength;

    public TextInputPurpose Purpose {
        get => IsPassword ? TextInputPurpose.Password : _purpose;
        set => _purpose = value;
    }

    public string SelectedText => SelectionLength == 0
        ? string.Empty
        : Text.Substring(SelectionStart, SelectionLength);

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

        _textRevision++;
        SynchronizeAccessibility();
        InvalidateLayout();
        TextChanged?.Invoke(Text);
    }

    public void Select(int start, int length) {
        _editing.SelectRange(start, length);
        InvalidatePaint();
    }

    public void SelectAll() {
        _editing.SelectAll();
        InvalidatePaint();
    }

    public bool HandleKey(KeyInputEvent input) {
        if (input.Phase != KeyPhase.Down || !IsEnabled) {
            return false;
        }

        if (_editing.HasComposition &&
            input.Key is Key.Backspace or Key.Delete or Key.Left or Key.Right or
                Key.Home or Key.End or Key.Enter or Key.Escape) {
            // The input method owns editing and navigation while a preedit is
            // active. In particular, Escape cancels the preedit instead of
            // closing the window that hosts this editor.
            return true;
        }

        var primaryModifier = input.Control || input.Meta;
        if (IsPrimaryShortcut(input, Key.A, "a")) {
            SelectAll();
            return true;
        }

        if (IsPrimaryShortcut(input, Key.C, "c")) {
            CopySelection();
            return true;
        }

        if (IsPrimaryShortcut(input, Key.X, "x")) {
            CutSelection();
            return true;
        }

        if (IsPrimaryShortcut(input, Key.V, "v")) {
            PasteText();
            return true;
        }

        switch (input.Key) {
            case Key.Backspace:
                if (IsReadOnly) {
                    return true;
                }

                if ((primaryModifier
                        ? _editing.DeleteWordBackward()
                        : _editing.DeleteBackward())) {
                    OnTextEdited();
                }

                return true;
            case Key.Delete:
                if (IsReadOnly) {
                    return true;
                }

                if ((primaryModifier
                        ? _editing.DeleteWordForward()
                        : _editing.DeleteForward())) {
                    OnTextEdited();
                }

                return true;
            case Key.Left:
                MoveLeft(primaryModifier, input.Shift);
                return true;
            case Key.Right:
                MoveRight(primaryModifier, input.Shift);
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

        return false;
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
                _editing.CommitComposition(
                    FilterControlCharacters(input.Text));
                OnTextEdited();
                return true;
            case TextCompositionPhase.Cancel:
                _editing.CancelComposition();
                break;
            default:
                return false;
        }

        _textRevision++;
        SynchronizeAccessibility();
        InvalidateLayout();
        return true;
    }

    public bool DeleteSurroundingText(TextInputDeleteSurroundingEvent input) {
        if (IsReadOnly || !IsEnabled ||
            !_editing.DeleteSurroundingText(
                input.BeforeUtf8Bytes,
                input.AfterUtf8Bytes)) {
            return false;
        }

        OnTextEdited();
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

        if (!IsEnabled) {
            _dragging = false;
            return;
        }

        if (phase == PointerPhase.Down) {
            var clickCount = NextClickCount(input);
            var caret = CaretFromX(inputEvent.LayoutX);
            if (clickCount == 2) {
                _editing.SelectWordAt(caret);
            } else if (clickCount == 3) {
                _editing.SelectAll();
            } else {
                _editing.MoveCaret(caret, extendSelection: false);
            }

            _dragging = clickCount < 3;
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
                var previousText = Text;
                if (_editing.SetMaxLength(
                        value is int maxLength ? maxLength : MaxLength)) {
                    _textRevision++;
                    SynchronizeAccessibility();
                    InvalidateLayout();
                    if (!string.Equals(previousText, Text, StringComparison.Ordinal)) {
                        TextChanged?.Invoke(Text);
                    }
                }

                break;
        }
    }

    private void MoveCaret(int index, bool extendSelection) {
        _editing.MoveCaret(index, extendSelection);
        InvalidatePaint();
    }

    private void MoveLeft(bool byWord, bool extendSelection) {
        if (!extendSelection && _editing.HasSelection) {
            MoveCaret(_editing.SelectionStart, extendSelection: false);
            return;
        }

        var index = byWord
            ? _editing.PreviousWordBoundary(_editing.CaretIndex)
            : _editing.PreviousTextElementStart(_editing.CaretIndex);
        MoveCaret(index, extendSelection);
    }

    private void MoveRight(bool byWord, bool extendSelection) {
        if (!extendSelection && _editing.HasSelection) {
            MoveCaret(
                _editing.SelectionStart + _editing.SelectionLength,
                extendSelection: false);
            return;
        }

        var index = byWord
            ? _editing.NextWordBoundary(_editing.CaretIndex)
            : _editing.NextTextElementStart(_editing.CaretIndex);
        MoveCaret(index, extendSelection);
    }

    private int NextClickCount(PointerInputEvent input) {
        var timestamp = input.Timestamp != 0
            ? input.Timestamp
            : Environment.TickCount64;
        var elapsed = timestamp - _lastClickTimestamp;
        var samePosition =
            Math.Abs(input.X - _lastClickX) <= MultiClickDistance &&
            Math.Abs(input.Y - _lastClickY) <= MultiClickDistance;
        _clickCount = elapsed >= 0 &&
                      elapsed <= MultiClickIntervalMilliseconds &&
                      samePosition
            ? (_clickCount % 3) + 1
            : 1;
        _lastClickTimestamp = timestamp;
        _lastClickX = input.X;
        _lastClickY = input.Y;
        return _clickCount;
    }

    private void ReplaceSelection(string value) {
        _editing.ReplaceSelection(value);
        OnTextEdited();
    }

    private void OnTextEdited() {
        _textRevision++;
        SynchronizeAccessibility();
        InvalidateLayout();
        TextChanged?.Invoke(Text);
    }

    private static bool IsPrimaryShortcut(
        KeyInputEvent input,
        Key key,
        string letter) {
        if (!input.Control && !input.Meta) {
            return false;
        }

        return input.Key == key ||
               input.Key == Key.Unknown &&
               input.Text?.Equals(letter, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void SynchronizeAccessibility() {
        if (string.IsNullOrEmpty(Accessibility.Name)) {
            Accessibility.Name = Placeholder;
        }

        if (!IsPassword) {
            Accessibility.Value = Text;
            return;
        }

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
