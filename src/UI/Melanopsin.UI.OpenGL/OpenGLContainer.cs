using Melanopsin.UI.Markup;

namespace Melanopsin.UI.OpenGL;

/// <summary>
/// Hosts native OpenGL content inside the normal layout tree. It renders to a
/// private WGL or EGL surface, reads the result back, and contributes a common
/// <see cref="DrawImageCommand"/> so the active UI backend handles final composition.
/// </summary>
[MarkupElement]
public sealed class OpenGLContainer : Control, IRenderCommandProvider, IDisposable {
    private IOpenGLSurface? _surface;
    private byte[]? _softwarePreview;
    private bool _disposed;

    public OpenGLContainer()
        : base("OpenGLContainer") {
        ClipToBounds = true;
    }

    public Color ClearColor { get; set; } = new Color(12, 18, 28);

    /// <summary>
    /// Optional OpenGL drawing callback. The native OpenGL context is current and the
    /// viewport already matches the control's pixel size.
    /// </summary>
    public Action<int, int>? RenderContent { get; set; }

    public void InvalidateContent() {
        Invalidate();
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var bounds = context.Bounds;
        var width = Math.Max(1, (int)MathF.Round(bounds.Width));
        var height = Math.Max(1, (int)MathF.Round(bounds.Height));
        var size = new PixelSize(width, height);
        ReadOnlyMemory<byte> pixels;
        if (RenderContent is not null &&
            (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())) {
            _surface ??= CreateSurface();
            pixels = _surface.Render(width, height, ClearColor, RenderContent);
        } else {
            pixels = RenderSoftwarePreview(size);
        }

        var stride = checked(width * 4);
        commands.Add(new DrawImageCommand(
            bounds,
            size,
            RenderPixelFormat.Bgra8888Premultiplied,
            stride,
            pixels,
            context.CornerRadius));
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        var surface = _surface;
        _surface = null;
        _softwarePreview = null;
        surface?.Dispose();
    }

    protected override void OnUnmounted() {
        var surface = _surface;
        _surface = null;
        surface?.Dispose();
    }

    private ReadOnlyMemory<byte> RenderSoftwarePreview(PixelSize size) {
        var length = checked(size.Width * size.Height * 4);
        if (_softwarePreview?.Length != length) {
            _softwarePreview = new byte[length];
        }

        for (var y = 0; y < size.Height; y++) {
            var green = (byte)Math.Clamp(
                ClearColor.Green + (y * 32 / size.Height),
                0,
                255);
            for (var x = 0; x < size.Width; x++) {
                var offset = ((y * size.Width) + x) * 4;
                _softwarePreview[offset] = ClearColor.Blue;
                _softwarePreview[offset + 1] = green;
                _softwarePreview[offset + 2] = ClearColor.Red;
                _softwarePreview[offset + 3] = byte.MaxValue;
            }
        }

        return _softwarePreview;
    }

    private static IOpenGLSurface CreateSurface() {
        if (OperatingSystem.IsWindows()) {
            return new WglSurface();
        }

        if (OperatingSystem.IsLinux()) {
            return new EglSurface();
        }

        throw new PlatformNotSupportedException(
            "Native OpenGL surfaces are supported on Windows and Linux Wayland.");
    }
}
