namespace Melanopsin.Graphics;

public readonly record struct Color {
    public Color(byte red, byte green, byte blue, byte alpha = byte.MaxValue) {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public byte Alpha { get; }
}
