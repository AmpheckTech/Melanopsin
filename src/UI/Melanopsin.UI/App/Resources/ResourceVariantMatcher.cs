// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

/// <summary>
/// Selects the most specific value for a requested variant. Culture falls back
/// through parent cultures before the invariant value. Theme, style, and display
/// scale fall back to their default value, but never to an unrelated value.
/// </summary>
internal static class ResourceVariantMatcher {
    public static ResourcePackValue? Select(
        IReadOnlyList<ResourcePackValue> values,
        ResourceVariant requested,
        ResourceDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(descriptor);

        ResourcePackValue? best = null;
        var bestRank = default((int Culture, int Theme, int Style, int DisplayScale));
        var hasBest = false;
        foreach (var value in values) {
            if (!TryRank(value.Variant, requested, descriptor, out var rank)) {
                continue;
            }

            if (!hasBest || CompareRanks(rank, bestRank) < 0) {
                best = value;
                bestRank = rank;
                hasBest = true;
            }
        }

        return best;
    }

    private static bool TryRank(
        ResourceVariant candidate,
        ResourceVariant requested,
        ResourceDescriptor descriptor,
        out (int Culture, int Theme, int Style, int DisplayScale) rank) {
        if (!TryRankCulture(
                candidate.Culture,
                requested.Culture,
                out var cultureRank) ||
            !TryRankText(
                candidate.Theme,
                requested.Theme,
                out var themeRank) ||
            !TryRankText(
                candidate.Style,
                requested.Style,
                out var styleRank) ||
            !TryRankScale(
                candidate.DisplayScale,
                requested.DisplayScale,
                out var scaleRank)) {
            rank = default;
            return false;
        }

        if ((descriptor.Variants & ResourceVariantKinds.Culture) == 0 &&
            candidate.Culture is not null) {
            rank = default;
            return false;
        }

        if ((descriptor.Variants & ResourceVariantKinds.Theme) == 0 &&
            candidate.Theme is not null) {
            rank = default;
            return false;
        }

        if ((descriptor.Variants & ResourceVariantKinds.Style) == 0 &&
            candidate.Style is not null) {
            rank = default;
            return false;
        }

        if ((descriptor.Variants & ResourceVariantKinds.DisplayScale) == 0 &&
            candidate.DisplayScale != 1.0f) {
            rank = default;
            return false;
        }

        rank = (cultureRank, themeRank, styleRank, scaleRank);
        return true;
    }

    private static int CompareRanks(
        (int Culture, int Theme, int Style, int DisplayScale) left,
        (int Culture, int Theme, int Style, int DisplayScale) right) {
        var result = left.Culture.CompareTo(right.Culture);
        if (result != 0) {
            return result;
        }

        result = left.Theme.CompareTo(right.Theme);
        if (result != 0) {
            return result;
        }

        result = left.Style.CompareTo(right.Style);
        return result != 0
            ? result
            : left.DisplayScale.CompareTo(right.DisplayScale);
    }

    private static bool TryRankCulture(
        string? candidate,
        string? requested,
        out int rank) {
        var requestedName = requested ?? string.Empty;
        var candidateName = candidate ?? string.Empty;
        if (requestedName.Length == 0) {
            rank = candidateName.Length == 0 ? 0 : -1;
            return rank == 0;
        }

        if (candidateName.Length == 0) {
            rank = GetCultureDepth(requestedName);
            return true;
        }

        rank = 0;
        foreach (var cultureName in EnumerateCultureAncestors(requestedName)) {
            if (string.Equals(
                    cultureName,
                    candidateName,
                    StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            rank++;
        }

        rank = -1;
        return false;
    }

    private static int GetCultureDepth(string name) {
        var depth = 0;
        foreach (var _ in EnumerateCultureAncestors(name)) {
            depth++;
        }

        return depth;
    }

    private static IEnumerable<string> EnumerateCultureAncestors(string name) {
        var current = name;
        while (current.Length != 0) {
            yield return current;
            var separator = current.LastIndexOf('-');
            if (separator <= 0) {
                yield break;
            }

            current = current[..separator];
        }
    }

    private static bool TryRankText(
        string? candidate,
        string? requested,
        out int rank) {
        if (requested is null) {
            rank = candidate is null ? 0 : -1;
            return rank == 0;
        }

        if (string.Equals(candidate, requested, StringComparison.Ordinal)) {
            rank = 0;
            return true;
        }

        rank = candidate is null ? 1 : -1;
        return rank == 1;
    }

    private static bool TryRankScale(
        float candidate,
        float requested,
        out int rank) {
        if (candidate.Equals(requested)) {
            rank = 0;
            return true;
        }

        rank = candidate.Equals(1.0f) ? 1 : -1;
        return rank == 1;
    }
}
