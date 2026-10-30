namespace Melanopsin.Graphics.Text;

public sealed class TextLine {
    public TextLine(
        IReadOnlyList<TextRun> runs,
        float width,
        float height,
        float ascent,
        float descent) {
        ArgumentNullException.ThrowIfNull(runs);
        Runs = runs;
        Width = width;
        Height = height;
        Ascent = ascent;
        Descent = descent;
    }

    public IReadOnlyList<TextRun> Runs { get; }

    public float Width { get; }

    public float Height { get; }

    public float Ascent { get; }

    public float Descent { get; }
}
