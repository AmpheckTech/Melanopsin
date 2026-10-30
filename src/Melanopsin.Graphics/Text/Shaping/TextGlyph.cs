namespace Melanopsin.Graphics.Text;

public readonly record struct TextGlyph(
    uint Id,
    int Cluster,
    float XAdvance,
    float YAdvance,
    float XOffset,
    float YOffset);
