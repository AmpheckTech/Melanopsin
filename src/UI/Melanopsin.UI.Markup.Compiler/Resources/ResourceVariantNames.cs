namespace Melanopsin.UI.Markup.Compiler.Resources;

internal static class ResourceVariantNames {
    public static bool TryParse(
        string value,
        out IReadOnlyList<string> variants,
        out string? error) {
        var parsed = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.Split(',')) {
            var name = item.Trim();
            if (name.Length == 0) {
                variants = Array.Empty<string>();
                error = "Resource variants cannot contain an empty name.";
                return false;
            }

            if (!IsKnown(name)) {
                variants = Array.Empty<string>();
                error = $"Unknown resource variant '{name}'.";
                return false;
            }

            if (!seen.Add(name)) {
                variants = Array.Empty<string>();
                error = $"Resource variant '{name}' is declared more than once.";
                return false;
            }

            parsed.Add(name);
        }

        variants = parsed;
        error = null;
        return true;
    }

    public static string ToCSharpExpression(IReadOnlyList<string> variants) {
        if (variants.Count == 0) {
            return "global::Melanopsin.UI.App.ResourceVariantKinds.None";
        }

        return string.Join(
            " | ",
            variants.Select(static name =>
                "global::Melanopsin.UI.App.ResourceVariantKinds." + name));
    }

    private static bool IsKnown(string name) {
        return name is "Culture" or "Theme" or "Style" or "DisplayScale";
    }
}
