namespace Melanopsin.Graphics.Text;

internal static class BidiImplicitLevels {
    public static void Resolve(Span<BidiCharacter> characters) {
        foreach (ref var character in characters) {
            if (character.IsRemoved) {
                continue;
            }

            if ((character.Level & 1) == 0) {
                if (character.Class == BidiClass.R) {
                    character.Level++;
                } else if (character.Class is BidiClass.AN or BidiClass.EN) {
                    character.Level += 2;
                }
            } else if (character.Class is
                       BidiClass.L or BidiClass.AN or BidiClass.EN) {
                character.Level++;
            }
        }
    }
}
