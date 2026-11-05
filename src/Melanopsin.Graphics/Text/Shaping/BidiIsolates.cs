// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal sealed class BidiIsolates {
    private readonly int[] _matchingPdi;
    private readonly int[] _matchingInitiator;

    private BidiIsolates(int[] matchingPdi, int[] matchingInitiator) {
        _matchingPdi = matchingPdi;
        _matchingInitiator = matchingInitiator;
    }

    public static BidiIsolates Build(ReadOnlySpan<BidiCharacter> characters) {
        var matchingPdi = new int[characters.Length];
        var matchingInitiator = new int[characters.Length];
        Array.Fill(matchingPdi, -1);
        Array.Fill(matchingInitiator, -1);
        var stack = new Stack<int>();
        for (var index = 0; index < characters.Length; index++) {
            if (IsInitiator(characters[index].OriginalClass)) {
                stack.Push(index);
            } else if (characters[index].OriginalClass == BidiClass.PDI && stack.Count > 0) {
                var initiator = stack.Pop();
                matchingPdi[initiator] = index;
                matchingInitiator[index] = initiator;
            }
        }

        return new BidiIsolates(matchingPdi, matchingInitiator);
    }

    public int MatchingPdi(int initiator) => _matchingPdi[initiator];

    public int MatchingInitiator(int pdi) => _matchingInitiator[pdi];

    public static bool IsInitiator(BidiClass value) {
        return value is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;
    }
}
