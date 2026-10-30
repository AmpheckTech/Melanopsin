namespace Melanopsin.UI.Input;

public sealed class KeyInputEvent {
    public KeyInputEvent(
        WindowId windowId,
        Key key,
        KeyPhase phase,
        bool shift = false,
        bool control = false,
        bool alt = false,
        bool meta = false,
        string? text = null,
        long timestamp = 0) {
        WindowId = windowId;
        Key = key;
        Phase = phase;
        Shift = shift;
        Control = control;
        Alt = alt;
        Meta = meta;
        Text = text;
        Timestamp = timestamp;
    }

    public WindowId WindowId { get; }

    public Key Key { get; }

    public KeyPhase Phase { get; }

    public bool Shift { get; }

    public bool Control { get; }

    public bool Alt { get; }

    public bool Meta { get; }

    public string? Text { get; }

    public long Timestamp { get; }
}
