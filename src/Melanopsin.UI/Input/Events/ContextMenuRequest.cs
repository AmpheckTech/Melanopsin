namespace Melanopsin.UI.Input;

/// <summary>
/// Describes a request to open a context menu at a window coordinate.
/// </summary>
public sealed class ContextMenuRequest {
    internal ContextMenuRequest(
        Node source,
        PointerInputEvent pointer) {
        Source = source;
        WindowId = pointer.WindowId;
        X = pointer.X;
        Y = pointer.Y;
        Device = pointer.Device;
        Timestamp = pointer.Timestamp;
        CurrentTarget = source;
    }

    public Node Source { get; }

    public Node CurrentTarget { get; internal set; }

    public WindowId WindowId { get; }

    public float X { get; }

    public float Y { get; }

    public InputDeviceKind Device { get; }

    public long Timestamp { get; }

    /// <summary>
    /// Stops the request from bubbling to ancestors.
    /// </summary>
    public bool Handled { get; set; }
}
