// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal static class BidiAlgorithm {
    public static BidiParagraph AnalyzeParagraph(
        string text,
        int start,
        int length,
        TextDirection requested) {
        var characters = BidiLayout.Decode(text.AsSpan(start, length));
        var isolates = BidiIsolates.Build(characters);
        int? explicitLevel = requested switch {
            TextDirection.LeftToRight => 0,
            TextDirection.RightToLeft => 1,
            _ => null,
        };
        var paragraphLevel = ResolveParagraphLevel(characters, isolates, explicitLevel);
        BidiExplicitResolver.Resolve(characters, paragraphLevel, isolates);
        var sequences = BidiSequenceBuilder.Build(characters, isolates, paragraphLevel);
        foreach (var sequence in sequences) {
            BidiWeakTypes.Resolve(characters, sequence);
            BidiNeutralTypes.Resolve(characters, sequence);
        }

        BidiImplicitLevels.Resolve(characters);
        BidiLevelResetter.ResetTrailingWhitespace(characters, paragraphLevel);
        var direction = paragraphLevel == 0
            ? TextDirection.LeftToRight
            : TextDirection.RightToLeft;
        return new BidiParagraph(start, length, direction, paragraphLevel, characters);
    }

    public static int ResolveParagraphLevel(
        ReadOnlySpan<BidiCharacter> characters,
        BidiIsolates isolates,
        int? explicitLevel) {
        if (explicitLevel.HasValue) {
            return explicitLevel.Value;
        }

        for (var index = 0; index < characters.Length; index++) {
            var value = characters[index].OriginalClass;
            if (BidiIsolates.IsInitiator(value)) {
                var pdi = isolates.MatchingPdi(index);
                if (pdi < 0) {
                    return 0;
                }

                index = pdi;
                continue;
            }

            if (value is BidiClass.R or BidiClass.AL) {
                return 1;
            }

            if (value == BidiClass.L) {
                return 0;
            }
        }

        return 0;
    }
}
