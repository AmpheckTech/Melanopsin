namespace Melanopsin.Graphics.Text;

internal readonly record struct TextToken(
    int Start,
    int Length,
    bool IsWhitespace,
    bool IsHardBreak);
