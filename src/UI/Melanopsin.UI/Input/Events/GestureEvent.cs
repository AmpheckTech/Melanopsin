namespace Melanopsin.UI.Input;

public sealed class GestureEvent {
    public GestureEvent(
        GestureKind kind,
        PointerInputEvent source,
        Node? target,
        float deltaX = 0,
        float deltaY = 0,
        float scale = 1,
        float rotation = 0) {
        Kind = kind;
        Source = source;
        Target = target;
        DeltaX = deltaX;
        DeltaY = deltaY;
        Scale = scale;
        Rotation = rotation;
    }

    public GestureKind Kind { get; }

    public PointerInputEvent Source { get; }

    public Node? Target { get; }

    public float DeltaX { get; }

    public float DeltaY { get; }

    public float Scale { get; }

    public float Rotation { get; }
}
