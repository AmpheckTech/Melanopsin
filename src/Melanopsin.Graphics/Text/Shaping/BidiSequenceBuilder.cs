// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal static class BidiSequenceBuilder {
    public static List<BidiRunSequence> Build(
        BidiCharacter[] characters,
        BidiIsolates isolates,
        int paragraphLevel) {
        var runs = BuildLevelRuns(characters);
        var runByCharacter = new int[characters.Length];
        Array.Fill(runByCharacter, -1);
        for (var runIndex = 0; runIndex < runs.Count; runIndex++) {
            foreach (var character in runs[runIndex]) {
                runByCharacter[character] = runIndex;
            }
        }

        var successor = new int[runs.Count];
        var predecessor = new int[runs.Count];
        Array.Fill(successor, -1);
        Array.Fill(predecessor, -1);
        for (var runIndex = 0; runIndex < runs.Count; runIndex++) {
            var run = runs[runIndex];
            var last = run[^1];
            if (!BidiIsolates.IsInitiator(characters[last].OriginalClass)) {
                continue;
            }

            var pdi = isolates.MatchingPdi(last);
            if (pdi < 0 || !IsFirstCharacterOfRun(runs[runIndex], runByCharacter, pdi, runIndex)) {
                continue;
            }

            var pdiRun = runByCharacter[pdi];
            if (pdiRun < 0 || predecessor[pdiRun] >= 0) {
                continue;
            }

            successor[runIndex] = pdiRun;
            predecessor[pdiRun] = runIndex;
        }

        var visited = new bool[runs.Count];
        var sequences = new List<BidiRunSequence>(runs.Count);
        for (var runIndex = 0; runIndex < runs.Count; runIndex++) {
            if (predecessor[runIndex] >= 0 || visited[runIndex]) {
                continue;
            }

            AppendSequence(runIndex);
        }

        for (var runIndex = 0; runIndex < runs.Count; runIndex++) {
            if (!visited[runIndex]) {
                AppendSequence(runIndex);
            }
        }

        return sequences;

        void AppendSequence(int firstRun) {
            var sequence = new List<int>();
            for (var runIndex = firstRun; runIndex >= 0 && !visited[runIndex];) {
                visited[runIndex] = true;
                sequence.AddRange(runs[runIndex]);
                runIndex = successor[runIndex];
            }

            var level = runs[firstRun].Count == 0
                ? paragraphLevel
                : characters[runs[firstRun][0]].Level;
            var sos = BoundaryLevel(sequence[0], -1);
            var last = sequence[^1];
            var eos = BidiIsolates.IsInitiator(characters[last].OriginalClass) &&
                      isolates.MatchingPdi(last) < 0
                ? DirectionForLevel(Math.Max(characters[last].Level, paragraphLevel))
                : BoundaryLevel(last, 1);
            sequences.Add(new BidiRunSequence(level, sos, eos, [.. sequence]));
        }

        BidiClass BoundaryLevel(int position, int step) {
            var adjacent = position + step;
            while (adjacent >= 0 && adjacent < characters.Length &&
                   characters[adjacent].IsRemoved) {
                adjacent += step;
            }

            var adjacentLevel = adjacent >= 0 && adjacent < characters.Length
                ? characters[adjacent].Level
                : paragraphLevel;
            var boundaryLevel = Math.Max(characters[position].Level, adjacentLevel);
            return DirectionForLevel(boundaryLevel);
        }

        static BidiClass DirectionForLevel(int level) =>
            (level & 1) == 0 ? BidiClass.L : BidiClass.R;
    }

    private static List<List<int>> BuildLevelRuns(BidiCharacter[] characters) {
        var runs = new List<List<int>>();
        List<int>? current = null;
        var currentLevel = -1;
        for (var index = 0; index < characters.Length; index++) {
            if (characters[index].IsRemoved) {
                continue;
            }

            if (current is null || characters[index].Level != currentLevel) {
                current = [];
                currentLevel = characters[index].Level;
                runs.Add(current);
            }

            current.Add(index);
        }

        return runs;
    }

    private static bool IsFirstCharacterOfRun(
        List<int> precedingRun,
        int[] runByCharacter,
        int character,
        int expectedRun) {
        var run = runByCharacter[character];
        if (run != expectedRun) {
            return run >= 0;
        }

        return precedingRun.Count > 0 && precedingRun[0] == character;
    }
}
