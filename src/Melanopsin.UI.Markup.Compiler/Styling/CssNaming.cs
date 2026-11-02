using System;
using System.Text;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static class CssNaming {
    public static string ClassName(string path) {
        var fileName = path.Substring(path.LastIndexOfAny(['/', '\\']) + 1);
        var dot = fileName.LastIndexOf('.');
        var stem = dot < 0 ? fileName : fileName.Substring(0, dot);
        return ToPascalIdentifier(stem) + "Styles";
    }

    public static string HintName(string path, string className) {
        var fileName = path.Substring(path.LastIndexOfAny(['/', '\\']) + 1);
        return className + "." + fileName + ".g.cs";
    }

    public static string QualifiedClassName(string rootNamespace, string path) {
        return "global::" +
               (string.IsNullOrWhiteSpace(rootNamespace) ? string.Empty : rootNamespace + ".") +
               ClassName(path);
    }

    public static string ToPascalIdentifier(string value) {
        var output = new StringBuilder(value.Length + 1);
        var capitalize = true;
        foreach (var character in value) {
            if (!char.IsLetterOrDigit(character)) {
                capitalize = true;
                continue;
            }

            output.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }

        if (output.Length == 0) {
            return "_StyleToken";
        }

        if (char.IsDigit(output[0])) {
            output.Insert(0, '_');
        }

        return output.ToString();
    }

    public static string PascalName(string value) {
        var output = new StringBuilder(value.Length);
        var capitalize = true;
        foreach (var character in value) {
            if (character == '-') {
                capitalize = true;
                continue;
            }

            output.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }

        return output.ToString();
    }
}
