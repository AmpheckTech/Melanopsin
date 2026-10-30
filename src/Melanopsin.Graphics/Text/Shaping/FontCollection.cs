using System.Text;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public sealed class FontCollection {
    private readonly Lock _gate = new();
    private readonly List<FontFace> _fonts = [];
    private readonly Dictionary<int, FontFace> _resolvedRunes = [];
    private FontFace[] _fontSnapshot;
    private long _version;

    public FontCollection(FontFace primary) {
        ArgumentNullException.ThrowIfNull(primary);
        Primary = primary;
        _fonts.Add(primary);
        _fontSnapshot = [primary];
    }

    public FontFace Primary { get; }

    public IReadOnlyList<FontFace> Fonts =>
        Array.AsReadOnly(Volatile.Read(ref _fontSnapshot));

    public long Version {
        get {
            lock (_gate) {
                return _version;
            }
        }
    }

    public void AddFallback(FontFace font) {
        ArgumentNullException.ThrowIfNull(font);
        lock (_gate) {
            if (!_fonts.Contains(font, ReferenceEqualityComparer.Instance)) {
                _fonts.Add(font);
                _fontSnapshot = [.. _fonts];
                _version++;
            }

            _resolvedRunes.Clear();
        }
    }

    public FontFace Resolve(Rune rune) {
        lock (_gate) {
            if (_resolvedRunes.TryGetValue(rune.Value, out var cached)) {
                return cached;
            }

            foreach (var font in _fonts) {
                if (font.GetGlyphId(rune) != 0) {
                    _resolvedRunes[rune.Value] = font;
                    return font;
                }
            }

            _resolvedRunes[rune.Value] = Primary;
            return Primary;
        }
    }
}
