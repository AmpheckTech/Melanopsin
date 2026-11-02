using System.Globalization;
using System.Text;
using Melanopsin.Graphics.Text.Fonts;
using Unicode.Bidi;

namespace Melanopsin.Graphics.Text;

public sealed partial class HarfBuzzTextShaper : ITextLayout, IDisposable {
    private readonly FontCollection _fonts;
    private readonly HarfBuzzGlyphShaper _glyphShaper;
    private readonly ReaderWriterLockSlim _lifetimeLock =
        new(LockRecursionPolicy.NoRecursion);

    public HarfBuzzTextShaper(FontCollection fonts) {
        ArgumentNullException.ThrowIfNull(fonts);
        _fonts = fonts;
        _glyphShaper = new HarfBuzzGlyphShaper(fonts);
    }

    public FontCollection Fonts => _fonts;

    private ShapedText ShapeText(
        string text,
        TextLayoutOptions options) {
        if (text.Length == 0) {
            var emptyMetrics = _glyphShaper.GetLineMetrics(
                options.Font ?? _fonts.Primary,
                options.FontSize);
            var emptyLine = new TextLine(
                [],
                0,
                emptyMetrics.Height,
                emptyMetrics.Ascent,
                emptyMetrics.Descent);
            return new ShapedText(
                text,
                [emptyLine],
                0,
                emptyMetrics.Height,
                emptyMetrics.Ascent,
                emptyMetrics.Descent);
        }

        var baseDirection = TextDirectionResolver.ResolveBase(
            options.Direction,
            text);
        var bidi = BidiInfo.Create(
            text,
            baseDirection == TextDirection.RightToLeft
                ? Level.Rtl()
                : Level.Ltr());
        var lines = new List<TextLine>();
        var pendingRuns = new List<TextRun>();
        var lineWidth = 0f;

        foreach (var token in TextLineBreaker.Tokenize(text)) {
            if (token.IsHardBreak) {
                lines.Add(CreateLine(
                    pendingRuns,
                    lineWidth,
                    baseDirection,
                    options.FontSize,
                    bidi,
                    options.Font));
                pendingRuns = [];
                lineWidth = 0;
                continue;
            }

            var tokenRuns = _glyphShaper.ShapeToken(
                text,
                token.Start,
                token.Length,
                options.FontSize,
                baseDirection,
                bidi,
                options.Font);
            var tokenWidth = tokenRuns.Sum(static run => run.Width);
            if (!token.IsWhitespace &&
                lineWidth > 0 &&
                lineWidth + tokenWidth > options.MaxWidth) {
                lines.Add(CreateLine(
                    pendingRuns,
                    lineWidth,
                    baseDirection,
                    options.FontSize,
                    bidi,
                    options.Font));
                pendingRuns = [];
                lineWidth = 0;
            }

            if (!token.IsWhitespace && tokenWidth > options.MaxWidth) {
                var tokenEnd = token.Start + token.Length;
                var elements = StringInfo.GetTextElementEnumerator(
                    text,
                    token.Start);
                while (elements.MoveNext() && elements.ElementIndex < tokenEnd) {
                    var element = (string)elements.Current;
                    var elementLength = Math.Min(
                        element.Length,
                        tokenEnd - elements.ElementIndex);
                    var elementRuns = _glyphShaper.ShapeToken(
                        text,
                        elements.ElementIndex,
                        elementLength,
                        options.FontSize,
                        baseDirection,
                        bidi,
                        options.Font);
                    var elementWidth = elementRuns.Sum(static run => run.Width);
                    var firstRuneStatus = Rune.DecodeFromUtf16(
                        element.AsSpan(0, elementLength),
                        out var firstRune,
                        out _);
                    var isWhitespace = firstRuneStatus ==
                        System.Buffers.OperationStatus.Done &&
                        Rune.IsWhiteSpace(firstRune);
                    if (!isWhitespace &&
                        lineWidth > 0 &&
                        lineWidth + elementWidth > options.MaxWidth) {
                        lines.Add(CreateLine(
                            pendingRuns,
                            lineWidth,
                            baseDirection,
                            options.FontSize,
                            bidi,
                            options.Font));
                        pendingRuns = [];
                        lineWidth = 0;
                    }

                    foreach (var run in elementRuns) {
                        pendingRuns.Add(run.Offset(lineWidth));
                    }

                    lineWidth += elementWidth;
                }

                continue;
            }

            foreach (var run in tokenRuns) {
                pendingRuns.Add(run.Offset(lineWidth));
            }

            lineWidth += tokenWidth;
        }

        if (pendingRuns.Count != 0 || lines.Count == 0) {
            lines.Add(CreateLine(
                pendingRuns,
                lineWidth,
                baseDirection,
                options.FontSize,
                bidi,
                options.Font));
        }

        var width = lines.Count == 0
            ? 0
            : lines.Max(static line => line.Width);
        var height = lines.Sum(static line => line.Height);
        var ascent = lines.Count == 0 ? 0 : lines.Max(static line => line.Ascent);
        var descent = lines.Count == 0 ? 0 : lines.Max(static line => line.Descent);
        return new ShapedText(
            text,
            lines,
            width,
            height,
            ascent,
            descent);
    }

    public TextMetrics MeasureText(string text, float fontSize) {
        return Layout(
            text,
            new TextLayoutOptions(
                fontSize,
                maxWidth: float.PositiveInfinity))
            .ToMetrics();
    }

    public TextCaretPosition HitTestCaret(
        string text,
        float x,
        in TextLayoutOptions options) {
        var layout = Layout(
            text,
            new TextLayoutOptions(
                options.FontSize,
                maxWidth: float.PositiveInfinity,
                direction: options.Direction,
                font: options.Font));
        var line = layout.Lines[0];
        var graphemeStarts = StringInfo.ParseCombiningCharacters(text);
        var best = new TextCaretPosition(0, 0, 0, 0, line.Height);
        var bestDistance = float.PositiveInfinity;
        var lineX = 0f;
        foreach (var run in line.Runs) {
            var glyphX = run.X;
            foreach (var glyph in run.Glyphs) {
                var beforeOffset = run.Direction == TextDirection.RightToLeft
                    ? NextGraphemeStart(graphemeStarts, glyph.Cluster, text.Length)
                    : glyph.Cluster;
                var afterOffset = run.Direction == TextDirection.RightToLeft
                    ? glyph.Cluster
                    : NextGraphemeStart(graphemeStarts, glyph.Cluster, text.Length);
                Consider(beforeOffset, glyphX);
                Consider(afterOffset, glyphX + glyph.XAdvance);
                glyphX += glyph.XAdvance;
            }

            lineX = Math.Max(lineX, glyphX);
        }

        Consider(text.Length, lineX);
        return best;

        void Consider(int offset, float candidateX) {
            var distance = Math.Abs(candidateX - x);
            if (distance >= bestDistance) {
                return;
            }

            bestDistance = distance;
            best = new TextCaretPosition(
                offset,
                candidateX,
                0,
                0,
                line.Height);
        }
    }

    private TextLine CreateLine(
        List<TextRun> runs,
        float width,
        TextDirection direction,
        float fontSize,
        BidiInfo bidi,
        FontFace? preferredFont) {
        var orderedRuns = runs;
        if (runs.Count > 1) {
            var levels = new Level[runs.Count];
            for (var index = 0; index < runs.Count; index++) {
                levels[index] = bidi.Levels[
                    Math.Clamp(runs[index].Start, 0, bidi.Levels.Count - 1)];
            }

            var order = BidiInfo.ReorderVisual(levels);
            orderedRuns = [.. runs];
            var x = 0f;
            for (var index = 0; index < orderedRuns.Count; index++) {
                var source = runs[order[index]];
                var run = source.Offset(x - source.X);
                orderedRuns[index] = run;
                x += run.Width;
            }
        }

        var primaryMetrics = _glyphShaper.GetLineMetrics(
            runs.Count == 0 ? preferredFont ?? _fonts.Primary : runs[0].Font,
            runs.Count == 0 ? fontSize : runs[0].FontSize);
        var ascent = runs.Count == 0
            ? primaryMetrics.Ascent
            : runs.Max(static run => run.Ascent);
        var descent = runs.Count == 0
            ? primaryMetrics.Descent
            : runs.Max(static run => run.Descent);
        return new TextLine(
            orderedRuns,
            Math.Max(0, width),
            Math.Max(0.001f, ascent + descent),
            ascent,
            descent);
    }

    private static int NextGraphemeStart(
        IReadOnlyList<int> starts,
        int offset,
        int textLength) {
        foreach (var start in starts) {
            if (start > offset) {
                return start;
            }
        }

        return textLength;
    }
}
