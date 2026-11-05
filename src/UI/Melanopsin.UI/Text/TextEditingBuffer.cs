using System.Globalization;
using System.Text;

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

    public void SelectRange(int start, int length) {
        var selectionStart = Math.Clamp(start, 0, _text.Length);
        var selectionEnd = (int)Math.Clamp(
            (long)selectionStart + Math.Max(0, length),
            0L,
            _text.Length);
        _selectionAnchor = selectionStart;
        _caret = selectionEnd;
    }

    public void SelectWordAt(int index) {
        if (_text.Length == 0) {
            return;
        }

        var starts = StringInfo.ParseCombiningCharacters(_text);
        var element = ElementAt(starts, index);
        if (element < 0) {
            return;
        }

        var isWord = IsWordElement(starts, element);
        var first = element;
        var last = element;
        while (first > 0 && IsWordElement(starts, first - 1) == isWord) {
            first--;
        }

        while (last + 1 < starts.Length && IsWordElement(starts, last + 1) == isWord) {
            last++;
        }

        var end = last + 1 < starts.Length ? starts[last + 1] : _text.Length;
        SelectRange(starts[first], end - starts[first]);
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

    public bool DeleteWordBackward() {
        if (HasSelection) {
            ReplaceRange(SelectionStart, SelectionLength, string.Empty);
            return true;
        }

        var previous = PreviousWordBoundary(_caret);
        if (previous >= _caret) {
            return false;
        }

        ReplaceRange(previous, _caret - previous, string.Empty);
        return true;
    }

    public bool DeleteWordForward() {
        if (HasSelection) {
            ReplaceRange(SelectionStart, SelectionLength, string.Empty);
            return true;
        }

        var next = NextWordBoundary(_caret);
        if (next <= _caret) {
            return false;
        }

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

    public bool DeleteSurroundingText(int beforeUtf8Bytes, int afterUtf8Bytes) {
        if (beforeUtf8Bytes <= 0 && afterUtf8Bytes <= 0) {
            return false;
        }

        var start = HasComposition ? CompositionStart : SelectionStart;
        var end = HasComposition
            ? CompositionStart + CompositionLength
            : SelectionStart + SelectionLength;
        start = Math.Clamp(start, 0, _text.Length);
        end = Math.Clamp(end, start, _text.Length);
        var beforeStart = PreviousUtf8Boundary(_text, start, beforeUtf8Bytes);
        var afterEnd = NextUtf8Boundary(_text, end, afterUtf8Bytes);
        if (beforeStart == start && afterEnd == end) {
            return false;
        }

        _text = _text.Remove(end, afterEnd - end);
        _text = _text.Remove(beforeStart, start - beforeStart);
        if (HasComposition) {
            _compositionStart = Math.Max(0, start - beforeStart);
            _compositionLength = Math.Clamp(
                _compositionLength,
                0,
                _text.Length - _compositionStart);
            _caret = _compositionStart + _compositionLength;
            _selectionAnchor = _caret;
        } else {
            _caret = beforeStart;
            _selectionAnchor = beforeStart;
        }

        return true;
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

    public int PreviousWordBoundary(int index) {
        if (index <= 0) {
            return 0;
        }

        var starts = StringInfo.ParseCombiningCharacters(_text);
        var element = LastElementStartingBefore(starts, index);
        while (element >= 0 && !IsWordElement(starts, element)) {
            element--;
        }

        if (element < 0) {
            return 0;
        }

        while (element > 0 && IsWordElement(starts, element - 1)) {
            element--;
        }

        return starts[element];
    }

    public int NextWordBoundary(int index) {
        if (index >= _text.Length) {
            return _text.Length;
        }

        var starts = StringInfo.ParseCombiningCharacters(_text);
        var element = FirstElementStartingAtOrAfter(starts, index);
        if (element < 0) {
            return _text.Length;
        }

        if (IsWordElement(starts, element)) {
            while (element < starts.Length && IsWordElement(starts, element)) {
                element++;
            }
        }

        while (element < starts.Length && !IsWordElement(starts, element)) {
            element++;
        }

        return element < starts.Length ? starts[element] : _text.Length;
    }

    internal void ReplaceRange(int start, int length, string replacement) {
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

    private static int PreviousUtf8Boundary(
        string text,
        int end,
        int byteCount) {
        var index = end;
        while (index > 0 && byteCount > 0) {
            var start = index - 1;
            if (char.IsLowSurrogate(text[start]) && start > 0) {
                start--;
            }

            byteCount -= Encoding.UTF8.GetByteCount(text.AsSpan(start, index - start));
            index = start;
        }

        return index;
    }

    private static int NextUtf8Boundary(
        string text,
        int start,
        int byteCount) {
        var index = start;
        while (index < text.Length && byteCount > 0) {
            var end = index + 1;
            if (char.IsHighSurrogate(text[index]) && end < text.Length) {
                end++;
            }

            byteCount -= Encoding.UTF8.GetByteCount(text.AsSpan(index, end - index));
            index = end;
        }

        return index;
    }

    private static int ElementAt(IReadOnlyList<int> starts, int index) {
        if (starts.Count == 0) {
            return -1;
        }

        if (index >= starts[starts.Count - 1]) {
            return starts.Count - 1;
        }

        for (var element = 0; element < starts.Count; element++) {
            if (starts[element] > index) {
                return Math.Max(0, element - 1);
            }
        }

        return starts.Count - 1;
    }

    private static int LastElementStartingBefore(IReadOnlyList<int> starts, int index) {
        for (var element = starts.Count - 1; element >= 0; element--) {
            if (starts[element] < index) {
                return element;
            }
        }

        return -1;
    }

    private static int FirstElementStartingAtOrAfter(
        IReadOnlyList<int> starts,
        int index) {
        for (var element = 0; element < starts.Count; element++) {
            if (starts[element] >= index) {
                return element;
            }
        }

        return -1;
    }

    private bool IsWordElement(IReadOnlyList<int> starts, int element) {
        var start = starts[element];
        if (IsWordCharacter(CharUnicodeInfo.GetUnicodeCategory(_text, start))) {
            return true;
        }

        if ((_text[start] != '\'' && _text[start] != '\u2019') ||
            element <= 0 ||
            element + 1 >= starts.Count) {
            return false;
        }

        return IsWordCharacter(
                   CharUnicodeInfo.GetUnicodeCategory(_text, starts[element - 1])) &&
               IsWordCharacter(
                   CharUnicodeInfo.GetUnicodeCategory(_text, starts[element + 1]));
    }

    private static bool IsWordCharacter(UnicodeCategory category) {
        return category is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.ConnectorPunctuation;
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
