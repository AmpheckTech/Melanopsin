// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal static class BidiNeutralTypes {
    private const int MaxBracketDepth = 63;

    public static void Resolve(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        ResolveBrackets(characters, sequence);
        ResolveNeutrals(characters, sequence);
    }

    private static void ResolveBrackets(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var pairs = FindBracketPairs(characters, sequence);
        var embeddingDirection = (sequence.Level & 1) == 0
            ? BidiClass.L
            : BidiClass.R;
        foreach (var (openingPosition, closingPosition) in pairs) {
            var hasEmbeddingDirection = false;
            var hasOppositeDirection = false;
            for (var position = openingPosition + 1; position < closingPosition; position++) {
                if (!TryStrongDirection(
                    characters[sequence.Characters[position]].Class,
                    out var direction)) {
                    continue;
                }

                hasEmbeddingDirection |= direction == embeddingDirection;
                hasOppositeDirection |= direction != embeddingDirection;
            }

            BidiClass resolved;
            if (hasEmbeddingDirection) {
                resolved = embeddingDirection;
            } else if (hasOppositeDirection) {
                var preceding = PrecedingStrong(characters, sequence, openingPosition);
                resolved = preceding == Opposite(embeddingDirection)
                    ? preceding
                    : embeddingDirection;
            } else {
                continue;
            }

            SetBracket(characters, sequence, openingPosition, resolved);
            SetBracket(characters, sequence, closingPosition, resolved);
        }
    }

    private static (int Opening, int Closing)[] FindBracketPairs(
        ReadOnlySpan<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var stack = new Stack<(int Expected, int Position)>();
        var pairs = new List<(int Opening, int Closing)>();
        for (var position = 0; position < sequence.Characters.Length; position++) {
            var character = characters[sequence.Characters[position]];
            if (character.Class != BidiClass.ON ||
                !BidiData.TryGetBracket(
                    character.CodePoint,
                    out var pairedCodePoint,
                    out var isOpen)) {
                continue;
            }

            if (isOpen) {
                if (stack.Count == MaxBracketDepth) {
                    return [];
                }

                stack.Push((pairedCodePoint, position));
                continue;
            }

            var matchDepth = 0;
            foreach (var opening in stack) {
                if (BracketMatches(opening.Expected, character.CodePoint)) {
                    break;
                }

                matchDepth++;
            }

            if (matchDepth == stack.Count) {
                continue;
            }

            var matched = stack.Peek();
            for (var depth = 0; depth <= matchDepth; depth++) {
                matched = stack.Pop();
            }

            pairs.Add((matched.Position, position));
        }

        pairs.Sort(static (left, right) => left.Opening.CompareTo(right.Opening));
        return [.. pairs];
    }

    private static bool BracketMatches(int expected, int closing) {
        return expected == closing ||
            (expected == 0x3009 && closing == 0x232A) ||
            (expected == 0x232A && closing == 0x3009);
    }

    private static void SetBracket(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence,
        int position,
        BidiClass direction) {
        characters[sequence.Characters[position]].Class = direction;
        for (var following = position + 1;
             following < sequence.Characters.Length &&
             characters[sequence.Characters[following]].OriginalClass == BidiClass.NSM;
             following++) {
            characters[sequence.Characters[following]].Class = direction;
        }
    }

    private static BidiClass PrecedingStrong(
        ReadOnlySpan<BidiCharacter> characters,
        BidiRunSequence sequence,
        int position) {
        for (var index = position - 1; index >= 0; index--) {
            if (TryStrongDirection(
                characters[sequence.Characters[index]].Class,
                out var direction)) {
                return direction;
            }
        }

        return sequence.Sos;
    }

    private static void ResolveNeutrals(
        Span<BidiCharacter> characters,
        BidiRunSequence sequence) {
        var values = sequence.Characters;
        for (var position = 0; position < values.Length;) {
            if (!IsNeutral(characters[values[position]].Class)) {
                position++;
                continue;
            }

            var end = position + 1;
            while (end < values.Length && IsNeutral(characters[values[end]].Class)) {
                end++;
            }

            var before = position > 0 &&
                TryStrongDirection(characters[values[position - 1]].Class, out var beforeDirection)
                    ? beforeDirection
                    : PrecedingStrong(characters, sequence, position);

            var after = sequence.Eos;
            for (var index = end; index < values.Length; index++) {
                if (TryStrongDirection(characters[values[index]].Class, out var direction)) {
                    after = direction;
                    break;
                }
            }

            if (before == after) {
                for (var index = position; index < end; index++) {
                    characters[values[index]].Class = before;
                }
            }

            position = end;
        }

        var embeddingDirection = (sequence.Level & 1) == 0
            ? BidiClass.L
            : BidiClass.R;
        foreach (var index in values) {
            if (IsNeutral(characters[index].Class)) {
                characters[index].Class = embeddingDirection;
            }
        }
    }

    private static bool TryStrongDirection(BidiClass value, out BidiClass direction) {
        if (value == BidiClass.L) {
            direction = BidiClass.L;
            return true;
        }

        if (value is BidiClass.R or BidiClass.EN or BidiClass.AN) {
            direction = BidiClass.R;
            return true;
        }

        direction = default;
        return false;
    }

    private static BidiClass Opposite(BidiClass value) =>
        value == BidiClass.L ? BidiClass.R : BidiClass.L;

    private static bool IsNeutral(BidiClass value) {
        return value is
            BidiClass.B or
            BidiClass.S or
            BidiClass.WS or
            BidiClass.ON or
            BidiClass.LRI or
            BidiClass.RLI or
            BidiClass.FSI or
            BidiClass.PDI;
    }
}
