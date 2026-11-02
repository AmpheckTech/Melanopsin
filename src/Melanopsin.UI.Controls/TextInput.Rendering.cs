using System.Globalization;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

public sealed partial class TextInput {
    private Rect _textBounds;
    private ITextLayout? _lastTextLayout;
    private FontFace? _lastFont;
    private float _lastFontSize;
    private float _scrollX;

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
