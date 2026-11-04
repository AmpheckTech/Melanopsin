namespace Melanopsin.Graphics.Text;

internal static class BidiWeakTypes {
    public static void Resolve(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        ResolveNonSpacingMarks(characters, sequence);
        ResolveEuropeanNumbersAfterArabic(characters, sequence);

        foreach (var index in sequence.Characters) {
            if (characters[index].Class == BidiClass.AL) {
                characters[index].Class = BidiClass.R;
            }
        }

        ResolveSeparators(characters, sequence);
        ResolveEuropeanTerminators(characters, sequence);

        foreach (var index in sequence.Characters) {
            if (characters[index].Class is BidiClass.ES or BidiClass.ET or BidiClass.CS) {
                characters[index].Class = BidiClass.ON;
            }
        }

        ResolveEuropeanNumbersAfterLeft(characters, sequence);
    }

    private static void ResolveNonSpacingMarks(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var previous = sequence.Sos;
        var previousWasIsolate = false;
        foreach (var index in sequence.Characters) {
            ref var character = ref characters[index];
            if (character.Class == BidiClass.NSM) {
                character.Class = previousWasIsolate ? BidiClass.ON : previous;
            }

            previous = character.Class;
            previousWasIsolate = character.OriginalClass is
                BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;
        }
    }

    private static void ResolveEuropeanNumbersAfterArabic(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var strong = sequence.Sos;
        foreach (var index in sequence.Characters) {
            ref var character = ref characters[index];
            if (character.Class == BidiClass.EN && strong == BidiClass.AL) {
                character.Class = BidiClass.AN;
            }

            if (character.Class is BidiClass.R or BidiClass.L or BidiClass.AL) {
                strong = character.Class;
            }
        }
    }

    private static void ResolveSeparators(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var values = sequence.Characters;
        for (var position = 1; position + 1 < values.Length; position++) {
            ref var character = ref characters[values[position]];
            var previous = characters[values[position - 1]].Class;
            var next = characters[values[position + 1]].Class;
            if (character.Class == BidiClass.ES &&
                previous == BidiClass.EN &&
                next == BidiClass.EN) {
                character.Class = BidiClass.EN;
            } else if (character.Class == BidiClass.CS && previous == next &&
                       previous is BidiClass.EN or BidiClass.AN) {
                character.Class = previous;
            }
        }
    }

    private static void ResolveEuropeanTerminators(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var values = sequence.Characters;
        for (var position = 0; position < values.Length;) {
            if (characters[values[position]].Class != BidiClass.ET) {
                position++;
                continue;
            }

            var end = position + 1;
            while (end < values.Length && characters[values[end]].Class == BidiClass.ET) {
                end++;
            }

            var previous = position == 0
                ? sequence.Sos
                : characters[values[position - 1]].Class;
            var next = end == values.Length
                ? sequence.Eos
                : characters[values[end]].Class;
            if (previous == BidiClass.EN || next == BidiClass.EN) {
                for (var index = position; index < end; index++) {
                    characters[values[index]].Class = BidiClass.EN;
                }
            }

            position = end;
        }
    }

    private static void ResolveEuropeanNumbersAfterLeft(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var strong = sequence.Sos;
        foreach (var index in sequence.Characters) {
            ref var character = ref characters[index];
            if (character.Class == BidiClass.EN && strong == BidiClass.L) {
                character.Class = BidiClass.L;
            }

            if (character.Class is BidiClass.L or BidiClass.R) {
                strong = character.Class;
            }
        }
    }
}
