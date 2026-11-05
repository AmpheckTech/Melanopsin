namespace Melanopsin.UI.Input;

public sealed class TextCompositionEvent {
    public TextCompositionEvent(
        WindowId windowId,
        TextCompositionPhase phase,
        string text) {
        ArgumentNullException.ThrowIfNull(text);
        WindowId = windowId;
        Phase = phase;
        Text = text;
    }

    public WindowId WindowId { get; }

    public TextCompositionPhase Phase { get; }

    public string Text { get; }
}
