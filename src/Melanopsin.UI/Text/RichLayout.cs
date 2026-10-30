namespace Melanopsin.UI;

internal sealed record RichLayout(
    IReadOnlyList<RichLine> Lines,
    float Width,
    float Height,
    float Ascent,
    float Descent);
