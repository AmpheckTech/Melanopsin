using Melanopsin.Graphics;

namespace Melanopsin.Graphics.Rendering;

public sealed class DamageRegionBuilder {
    private readonly PixelSize _size;
    private readonly List<PixelRect> _rects = [];
    private bool _isFull;

    public DamageRegionBuilder(PixelSize size) {
        _size = size;
    }

    public bool IsEmpty => !_isFull && _rects.Count == 0;

    public bool IsFull => _isFull;

    public void MarkFull() {
        _isFull = true;
        _rects.Clear();
    }

    public void Add(Rect bounds) {
        if (_isFull || bounds.IsEmpty) {
            return;
        }

        var left = Math.Clamp(
            (int)MathF.Floor(bounds.X),
            0,
            _size.Width);
        var top = Math.Clamp(
            (int)MathF.Floor(bounds.Y),
            0,
            _size.Height);
        var right = Math.Clamp(
            (int)MathF.Ceiling(bounds.Right),
            0,
            _size.Width);
        var bottom = Math.Clamp(
            (int)MathF.Ceiling(bounds.Bottom),
            0,
            _size.Height);
        if (right <= left || bottom <= top) {
            return;
        }

        var added = new PixelRect(left, top, right - left, bottom - top);
        if (added == PixelRect.FromSize(_size)) {
            MarkFull();
            return;
        }

        for (var index = 0; index < _rects.Count;) {
            if (!_rects[index].Touches(added)) {
                index++;
                continue;
            }

            added = added.Union(_rects[index]);
            _rects.RemoveAt(index);
            index = 0;
        }

        if (added == PixelRect.FromSize(_size)) {
            MarkFull();
            return;
        }

        _rects.Add(added);
    }

    public DamageRegion Build() {
        return _isFull
            ? DamageRegion.Full(_size)
            : new DamageRegion(_size, _rects);
    }
}
