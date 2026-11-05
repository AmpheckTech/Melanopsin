// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal static class BidiExplicitResolver {
    private const int MaxDepth = 125;

    public static void Resolve(
        Span<BidiCharacter> characters,
        int paragraphLevel,
        BidiIsolates isolates) {
        var stack = new Stack<DirectionalStatus>();
        stack.Push(new DirectionalStatus(paragraphLevel, Override.Neutral, false));
        var overflowIsolateCount = 0;
        var overflowEmbeddingCount = 0;
        var validIsolateCount = 0;

        for (var index = 0; index < characters.Length; index++) {
            ref var character = ref characters[index];
            var current = stack.Peek();
            switch (character.Class) {
                case BidiClass.RLE:
                case BidiClass.RLO:
                case BidiClass.LRE:
                case BidiClass.LRO:
                    character.Level = (byte)current.Level;
                    var rightToLeft = character.Class is BidiClass.RLE or BidiClass.RLO;
                    var newLevel = rightToLeft
                        ? LeastGreaterOdd(current.Level)
                        : LeastGreaterEven(current.Level);
                    if (newLevel <= MaxDepth &&
                        overflowIsolateCount == 0 &&
                        overflowEmbeddingCount == 0) {
                        var directionalOverride = character.Class switch {
                            BidiClass.RLO => Override.RightToLeft,
                            BidiClass.LRO => Override.LeftToRight,
                            _ => Override.Neutral,
                        };
                        stack.Push(new DirectionalStatus(
                            newLevel,
                            directionalOverride,
                            false));
                    } else if (overflowIsolateCount == 0) {
                        overflowEmbeddingCount++;
                    }

                    break;
                case BidiClass.RLI:
                case BidiClass.LRI:
                case BidiClass.FSI:
                    var isolateLevel = character.Class;
                    if (character.Class == BidiClass.FSI) {
                        isolateLevel = ResolveFsiDirection(characters, index, isolates);
                        character.Class = isolateLevel;
                    }

                    character.Level = (byte)current.Level;
                    current.Override.Apply(ref character);
                    newLevel = isolateLevel == BidiClass.RLI
                        ? LeastGreaterOdd(current.Level)
                        : LeastGreaterEven(current.Level);
                    if (newLevel <= MaxDepth &&
                        overflowIsolateCount == 0 &&
                        overflowEmbeddingCount == 0) {
                        validIsolateCount++;
                        stack.Push(new DirectionalStatus(newLevel, Override.Neutral, true));
                    } else {
                        overflowIsolateCount++;
                    }

                    break;
                case BidiClass.PDI:
                    if (overflowIsolateCount > 0) {
                        overflowIsolateCount--;
                    } else if (validIsolateCount > 0) {
                        overflowEmbeddingCount = 0;
                        while (!stack.Peek().Isolate) {
                            stack.Pop();
                        }

                        stack.Pop();
                        validIsolateCount--;
                    }

                    current = stack.Peek();
                    character.Level = (byte)current.Level;
                    current.Override.Apply(ref character);
                    break;
                case BidiClass.PDF:
                    character.Level = (byte)current.Level;
                    if (overflowIsolateCount > 0) {
                        break;
                    }

                    if (overflowEmbeddingCount > 0) {
                        overflowEmbeddingCount--;
                    } else if (!current.Isolate && stack.Count > 1) {
                        stack.Pop();
                    }

                    break;
                case BidiClass.B:
                    character.Level = (byte)paragraphLevel;
                    break;
                case BidiClass.BN:
                    character.Level = (byte)current.Level;
                    break;
                default:
                    character.Level = (byte)current.Level;
                    current.Override.Apply(ref character);
                    break;
            }
        }
    }

    private static BidiClass ResolveFsiDirection(
        ReadOnlySpan<BidiCharacter> characters,
        int fsi,
        BidiIsolates isolates) {
        var end = isolates.MatchingPdi(fsi);
        if (end < 0) {
            end = characters.Length;
        }

        for (var index = fsi + 1; index < end; index++) {
            var value = characters[index].OriginalClass;
            if (BidiIsolates.IsInitiator(value)) {
                var pdi = isolates.MatchingPdi(index);
                if (pdi < 0) {
                    return BidiClass.LRI;
                }

                index = pdi;
                continue;
            }

            if (value is BidiClass.R or BidiClass.AL) {
                return BidiClass.RLI;
            }

            if (value == BidiClass.L) {
                return BidiClass.LRI;
            }
        }

        return BidiClass.LRI;
    }

    private static int LeastGreaterOdd(int level) => (level + 1) | 1;

    private static int LeastGreaterEven(int level) => (level + 2) & ~1;

    private readonly record struct DirectionalStatus(
        int Level,
        Override Override,
        bool Isolate);

    private enum Override {
        Neutral,
        LeftToRight,
        RightToLeft,
    }

    private static void Apply(this Override value, ref BidiCharacter character) {
        if (value == Override.LeftToRight) {
            character.Class = BidiClass.L;
        } else if (value == Override.RightToLeft) {
            character.Class = BidiClass.R;
        }
    }
}
