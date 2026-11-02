using System;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal readonly struct CssTokenReference {
    private CssTokenReference(string name) {
        Name = name;
    }

    public string Name { get; }

    public static bool TryParse(string value, out CssTokenReference reference) {
        var normalized = value.Trim();
        if (normalized.Length > 1 && normalized[0] == '$') {
            var candidate = normalized.Substring(1);
            if (IsIdentifier(candidate)) {
                reference = new CssTokenReference(candidate);
                return true;
            }
        }

        reference = default;
        return false;
    }

    private static bool IsIdentifier(string value) {
        if (value.Length == 0 || (!char.IsLetter(value[0]) && value[0] != '_')) {
            return false;
        }

        foreach (var character in value) {
            if (!char.IsLetterOrDigit(character) && character is not ('_' or '-')) {
                return false;
            }
        }

        return true;
    }
}
