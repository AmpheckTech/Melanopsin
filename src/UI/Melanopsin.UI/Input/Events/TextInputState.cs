namespace Melanopsin.UI.Input;

public readonly record struct TextInputState(
    WindowId WindowId,
    long EditorId,
    string Text,
    int CaretIndex,
    int SelectionStart,
    int SelectionLength,
    Rect CursorBounds,
    TextInputPurpose Purpose = TextInputPurpose.Text,
    bool IsReadOnly = false,
    bool IsSensitive = false);
