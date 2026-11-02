using System.Text;
using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public sealed class FontCollection {
    private readonly Lock _gate = new();
    private readonly List<FontFace> _fonts = [];
    private readonly Dictionary<int, FontFace> _resolvedRunes = [];
    private readonly Dictionary<int, FontFace> _resolvedEmojiRunes = [];
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
            _resolvedEmojiRunes.Clear();
        }
    }

    public FontFace Resolve(Rune rune) {
        return Resolve(rune, preferEmoji: false);
    }

    /// <summary>
    /// Chooses the face that draws <paramref name="rune"/>. With
    /// <paramref name="preferEmoji"/> a colour face wins even when an earlier
    /// face carries a monochrome glyph.
    /// </summary>
    public FontFace Resolve(Rune rune, bool preferEmoji) {
        lock (_gate) {
            var cache = preferEmoji ? _resolvedEmojiRunes : _resolvedRunes;
            if (cache.TryGetValue(rune.Value, out var cached)) {
                return cached;
            }

            FontFace? plainMatch = null;
            foreach (var font in _fonts) {
                if (font.GetGlyphId(rune) == 0) {
                    continue;
                }

                if (!preferEmoji || font.HasColorGlyphs) {
                    cache[rune.Value] = font;
                    return font;
                }

                plainMatch ??= font;
            }

            var resolved = plainMatch ?? Primary;
            cache[rune.Value] = resolved;
            return resolved;
        }
    }
}
