namespace Melanopsin.Graphics.Text;

public readonly record struct TextCaretPosition(
    int TextOffset,
    float X,
    int LineIndex,
    float LineTop,
    float LineHeight);
