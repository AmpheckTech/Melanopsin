namespace Melanopsin.UI;

internal sealed record RichLine(
    IReadOnlyList<ColoredRun> Run,
    float Width,
    float Height,
    float Ascent,
    float Descent);
