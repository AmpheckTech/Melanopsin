using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class DamageRegion {
    private readonly PixelRect[] _rects;

    public DamageRegion(PixelSize size, IReadOnlyList<PixelRect> rects) {
        ArgumentNullException.ThrowIfNull(rects);
        Size = size;
        _rects = [.. rects];
    }

    public PixelSize Size { get; }

    public IReadOnlyList<PixelRect> Rects => _rects;

    public bool IsEmpty => _rects.Length == 0;

    public bool IsFull => _rects.Length == 1 &&
        _rects[0] == PixelRect.FromSize(Size);

    public static DamageRegion Full(PixelSize size) {
        return new DamageRegion(size, [PixelRect.FromSize(size)]);
    }

    public static DamageRegion None(PixelSize size) {
        return new DamageRegion(size, []);
    }
}
