using System.Text;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation;

internal static class MarkupNaming {
    public static string ComponentClassName(MarkupDocument document) {
        return document.ComponentName ?? FromFileStem(document.Path);
    }

    private static string FromFileStem(string path) {
        var fileName = path.Substring(path.LastIndexOfAny(['/', '\\']) + 1);
        var stem = fileName.Substring(0, Math.Max(0, fileName.Length - 4));
        var result = new StringBuilder(stem.Length);
        var capitalize = true;
        foreach (var character in stem) {
            if (!char.IsLetterOrDigit(character)) {
                capitalize = true;
                continue;
            }

            result.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }

        if (result.Length == 0) {
            return "MarkupView";
        }

        if (char.IsDigit(result[0])) {
            result.Insert(0, '_');
        }

        return result.ToString();
    }
}
