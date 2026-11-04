using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandPendingFrame {
    private bool _isOccupied;

    public RenderFrameLease Frame { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int LogicalWidth { get; private set; }

    public int LogicalHeight { get; private set; }

    public float DisplayScale { get; private set; }

    public bool IsOccupied => _isOccupied;

    public bool IsLeaseValid => _isOccupied && Frame.IsValid;

    public void Set(
        RenderFrameLease frame,
        int logicalWidth,
        int logicalHeight,
        float displayScale) {
        if (IsOccupied) {
            throw new InvalidOperationException(
                "The pending frame slot is already occupied.");
        }

        var surface = frame.Surface;
        Frame = frame;
        _isOccupied = true;
        Width = surface.Size.Width;
        Height = surface.Size.Height;
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        DisplayScale = displayScale;
    }

    public void Clear() {
        var frame = DetachWithoutRelease();
        if (frame.IsValid) {
            frame.Release();
        }
    }

    public RenderFrameLease DetachWithoutRelease() {
        if (!_isOccupied) {
            Reset();
            return default;
        }

        var frame = Frame;
        Reset();
        return frame;
    }

    private void Reset() {
        _isOccupied = false;
        Frame = default;
        Width = 0;
        Height = 0;
        LogicalWidth = 0;
        LogicalHeight = 0;
        DisplayScale = 0;
    }
}
