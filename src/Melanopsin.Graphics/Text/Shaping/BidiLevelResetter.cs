namespace Melanopsin.Graphics.Text;

internal static class BidiLevelResetter {
    public static void ResetTrailingWhitespace(
        Span<BidiCharacter> characters,
        int paragraphLevel) {
        var reset = true;
        for (var index = characters.Length - 1; index >= 0; index--) {
            ref var character = ref characters[index];
            if (character.IsRemoved) {
                continue;
            }

            if (character.OriginalClass is BidiClass.B or BidiClass.S) {
                character.Level = (byte)paragraphLevel;
                reset = true;
                continue;
            }

            if (reset && IsWhitespaceOrIsolate(character.OriginalClass)) {
                character.Level = (byte)paragraphLevel;
                continue;
            }

            reset = false;
        }
    }

    private static bool IsWhitespaceOrIsolate(BidiClass value) {
        return value is
            BidiClass.WS or
            BidiClass.LRI or
            BidiClass.RLI or
            BidiClass.FSI or
            BidiClass.PDI;
    }
}
