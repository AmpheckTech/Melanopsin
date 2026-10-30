namespace Melanopsin.UI.Styling;

public static class StyleSheetParser {
    public static StyleSheet Parse(string source) {
        ArgumentNullException.ThrowIfNull(source);
        var sheet = new StyleSheet();
        var index = 0;
        while (index < source.Length) {
            SkipTrivia(source, ref index);
            if (index >= source.Length) {
                break;
            }

            var selectorStart = index;
            while (index < source.Length && source[index] != '{') {
                index++;
            }

            if (index >= source.Length) {
                throw new FormatException("Style rule is missing '{'.");
            }

            var selector = source[selectorStart..index].Trim();
            index++;
            var declarationStart = index;
            while (index < source.Length && source[index] != '}') {
                index++;
            }

            if (index >= source.Length) {
                throw new FormatException("Style rule is missing '}'.");
            }

            var declarations = ParseDeclarations(
                source[declarationStart..index]);
            index++;
            sheet.Add(new StyleRule(selector, declarations));
        }

        return sheet;
    }

    private static Dictionary<string, string> ParseDeclarations(string source) {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in source.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            var separator = declaration.IndexOf(':');
            if (separator <= 0 || separator == declaration.Length - 1) {
                continue;
            }

            var name = declaration[..separator].Trim();
            var value = declaration[(separator + 1)..].Trim();
            declarations[name] = value;
        }

        return declarations;
    }

    private static void SkipTrivia(string source, ref int index) {
        while (index < source.Length) {
            if (char.IsWhiteSpace(source[index])) {
                index++;
                continue;
            }

            if (index + 1 < source.Length &&
                source[index] == '/' &&
                source[index + 1] == '*') {
                var end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = end < 0 ? source.Length : end + 2;
                continue;
            }

            break;
        }
    }
}
