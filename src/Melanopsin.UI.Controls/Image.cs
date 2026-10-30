using Melanopsin.UI.Accessibility;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public sealed class Image : Control, IRenderCommandProvider {
    private byte[] _pixels = [];
    private ImageResource? _source;
    private PixelSize _pixelSize = new(1, 1);
    private RenderPixelFormat _pixelFormat = RenderPixelFormat.Bgra8888Premultiplied;
    private int _stride = 4;

    public Image()
        : base("Image") {
        IsRepaintBoundary = true;
        Accessibility.Role = AccessibilityRole.Image;
    }

    [MarkupAttribute("source")]
    public ImageResource? Source {
        get => _source;
        set {
            if (ReferenceEquals(_source, value)) {
                return;
            }

            _source = value;
            if (value is not null) {
                _pixels = [];
            }

            InvalidatePaint();
        }
    }

    public void SetPixels(
        PixelSize size,
        ReadOnlySpan<byte> pixels,
        RenderPixelFormat pixelFormat = RenderPixelFormat.Bgra8888Premultiplied,
        int? stride = null) {
        var resolvedStride = stride ?? size.Width * 4;
        if (resolvedStride < size.Width * 4 ||
            pixels.Length < resolvedStride * size.Height) {
            throw new ArgumentException("The image pixel buffer is incomplete.", nameof(pixels));
        }

        _source = null;
        _pixelSize = size;
        _stride = resolvedStride;
        _pixelFormat = pixelFormat;
        _pixels = pixels[..(resolvedStride * size.Height)].ToArray();
        InvalidatePaint();
    }

    public void AppendRenderCommands(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (_source is not null) {
            commands.Add(new DrawImageResourceCommand(
                context.Bounds,
                _source,
                context.CornerRadius));
            return;
        }

        if (_pixels.Length == 0) {
            return;
        }

        commands.Add(new DrawImageCommand(
            context.Bounds,
            _pixelSize,
            _pixelFormat,
            _stride,
            _pixels,
            context.CornerRadius));
    }

    protected override void OnSetAttribute(string name, object? value) {
        base.OnSetAttribute(name, value);
        if (name == "source") {
            Source = value as ImageResource;
        }
    }

    protected override InvalidationKind ClassifyAttributeChange(string name) {
        return name == "source"
            ? InvalidationKind.Paint
            : base.ClassifyAttributeChange(name);
    }
}
