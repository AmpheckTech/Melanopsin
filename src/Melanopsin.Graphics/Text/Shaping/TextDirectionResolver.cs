using System.Text;
using Unicode.Bidi;

namespace Melanopsin.Graphics.Text;

internal static class TextDirectionResolver {
    public static TextDirection ResolveBase(TextDirection requested, string text) {
        if (requested != TextDirection.Auto) {
            return requested;
        }

        return Bidi.GetBaseDirectionFull(text) == Unicode.Bidi.Direction.Rtl
            ? TextDirection.RightToLeft
            : TextDirection.LeftToRight;
    }

    public static TextDirection ResolveRune(
        Rune rune,
        int index,
        TextDirection baseDirection,
        BidiInfo bidi) {
        if (index >= 0 && index < bidi.Levels.Count) {
            return bidi.Levels[index].IsRtl()
                ? TextDirection.RightToLeft
                : TextDirection.LeftToRight;
        }

        return IsRightToLeft(rune)
            ? TextDirection.RightToLeft
            : baseDirection;
    }

    private static bool IsRightToLeft(Rune rune) {
        return rune.Value is
            >= 0x0590 and <= 0x08FF or
            >= 0xFB1D and <= 0xFDFF or
            >= 0xFE70 and <= 0xFEFF or
            >= 0x10800 and <= 0x10FFF or
            >= 0x1E800 and <= 0x1EEFF;
    }
}
