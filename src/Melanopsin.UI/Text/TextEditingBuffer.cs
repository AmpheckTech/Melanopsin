using System.Globalization;

namespace Melanopsin.UI;

/// <summary>
/// Owns the editable text state independently from control input, rendering,
/// and accessibility adaptation.
/// </summary>
internal sealed class TextEditingBuffer {
    private int _caret;
    private int _selectionAnchor;
    private string _text = string.Empty;
    private string _composition = string.Empty;
    private int _compositionStart;
    private int _compositionLength;
    private bool _hasComposition;

    public int MaxLength { get; private set; }

    public string Text => _text;

    public int CaretIndex => _caret;

    public int SelectionStart => Math.Min(_caret, _selectionAnchor);

    public int SelectionLength => Math.Abs(_caret - _selectionAnchor);

    public bool HasSelection => SelectionLength > 0;

    public string CompositionText => _composition;

    public bool HasComposition => _hasComposition;

    public int CompositionStart => _compositionStart;

    public int CompositionLength => _compositionLength;

    public string TextWithComposition {
        get {
            if (!_hasComposition) {
                return _text;
            }

            var start = Math.Clamp(_compositionStart, 0, _text.Length);
            var length = Math.Clamp(
                _compositionLength,
                0,
                _text.Length - start);
            return _text
                .Remove(start, length)
                .Insert(start, _composition);
        }
    }

    /// <summary>
    /// Updates the limit and returns whether the text or active composition
    /// changed.
    /// </summary>
    public bool SetMaxLength(int maxLength) {
        var next = Math.Max(0, maxLength);
        if (MaxLength == next) {
            return false;
        }

        MaxLength = next;
        var compositionChanged = ClearComposition();
        if (MaxLength == 0 || CountTextElements(_text) <= MaxLength) {
            return compositionChanged;
        }

        return SetText(TruncateToMaxElements(_text)) || compositionChanged;
    }

    public bool SetText(string value) {
        value ??= string.Empty;
        var compositionChanged = ClearComposition();
        if (MaxLength > 0) {
            value = TruncateToMaxElements(value);
        }

        if (string.Equals(_text, value, StringComparison.Ordinal)) {
            return compositionChanged;
        }

        _text = value;
        _caret = Math.Clamp(_caret, 0, _text.Length);
        _selectionAnchor = Math.Clamp(_selectionAnchor, 0, _text.Length);
        return true;
    }

    public void MoveCaret(int index, bool extendSelection) {
        _caret = Math.Clamp(index, 0, _text.Length);
        if (!extendSelection) {
            _selectionAnchor = _caret;
        }
    }

    public void CollapseSelection() {
        _selectionAnchor = _caret;
    }

    public void SelectAll() {
        _selectionAnchor = 0;
        _caret = _text.Length;
    }

    public bool DeleteBackward() {
        if (HasSelection) {
            ReplaceRange(SelectionStart, SelectionLength, string.Empty);
            return true;
        }

        if (_caret <= 0) {
            return false;
        }

        var previous = PreviousTextElementStart(_caret);
        ReplaceRange(previous, _caret - previous, string.Empty);
        return true;
    }

    public bool DeleteForward() {
        if (HasSelection) {
            ReplaceRange(SelectionStart, SelectionLength, string.Empty);
            return true;
        }

        if (_caret >= _text.Length) {
            return false;
        }

        var next = NextTextElementStart(_caret);
        ReplaceRange(_caret, next - _caret, string.Empty);
        return true;
    }

    public void ReplaceSelection(string replacement) {
        ReplaceRange(SelectionStart, SelectionLength, replacement);
    }

    public void BeginComposition(string text) {
        _compositionStart = SelectionStart;
        _compositionLength = SelectionLength;
        _composition = text ?? string.Empty;
        _hasComposition = true;
    }

    public void UpdateComposition(string text) {
        if (!_hasComposition) {
            BeginComposition(text);
            return;
        }

        _composition = text ?? string.Empty;
    }

    public void CommitComposition(string text) {
        var start = _hasComposition ? _compositionStart : SelectionStart;
        var length = _hasComposition ? _compositionLength : SelectionLength;
        ClearComposition();
        ReplaceRange(start, length, text);
    }

    public void CancelComposition() {
        ClearComposition();
    }

    public int PreviousTextElementStart(int index) {
        if (index <= 0) {
            return 0;
        }

        var starts = StringInfo.ParseCombiningCharacters(_text);
        for (var startIndex = starts.Length - 1; startIndex >= 0; startIndex--) {
            if (starts[startIndex] < index) {
                return starts[startIndex];
            }
        }

        return 0;
    }

    public int NextTextElementStart(int index) {
        if (index >= _text.Length) {
            return _text.Length;
        }

        var starts = StringInfo.ParseCombiningCharacters(_text);
        foreach (var start in starts) {
            if (start > index) {
                return start;
            }
        }

        return _text.Length;
    }

    private void ReplaceRange(int start, int length, string replacement) {
        replacement ??= string.Empty;
        var next = _text.Remove(start, length).Insert(start, replacement);
        if (MaxLength > 0) {
            next = TruncateToMaxElements(next);
        }

        _text = next;
        _caret = Math.Min(next.Length, start + replacement.Length);
        _caret = Math.Clamp(_caret, 0, next.Length);
        _selectionAnchor = _caret;
    }

    private string TruncateToMaxElements(string value) {
        if (MaxLength <= 0 || CountTextElements(value) <= MaxLength) {
            return value;
        }

        var starts = StringInfo.ParseCombiningCharacters(value);
        return value[..starts[MaxLength]];
    }

    private static int CountTextElements(string value) {
        return value.Length == 0
            ? 0
            : StringInfo.ParseCombiningCharacters(value).Length;
    }

    private bool ClearComposition() {
        if (!_hasComposition &&
            _composition.Length == 0 &&
            _compositionLength == 0) {
            return false;
        }

        _hasComposition = false;
        _composition = string.Empty;
        _compositionStart = SelectionStart;
        _compositionLength = 0;
        return true;
    }
}
