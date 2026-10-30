namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandPendingFrame {
    public WaylandPendingFrame(
        WindowDescriptor descriptor,
        RenderFrame frame,
        int logicalWidth,
        int logicalHeight,
        float displayScale) {
        Descriptor = descriptor;
        Frame = frame;
        Width = frame.Descriptor.Size.Width;
        Height = frame.Descriptor.Size.Height;
        Stride = frame.Stride;
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        DisplayScale = displayScale;
    }

    public WindowDescriptor Descriptor { get; }

    public RenderFrame Frame { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public int LogicalWidth { get; }

    public int LogicalHeight { get; }

    public float DisplayScale { get; }

    public ReadOnlyMemory<byte> Pixels => Frame.Pixels;
}
