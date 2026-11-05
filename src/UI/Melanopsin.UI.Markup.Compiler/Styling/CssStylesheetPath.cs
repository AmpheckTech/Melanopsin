using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static class CssStylesheetPath {
    public static StringComparer Comparer { get; } = Path.DirectorySeparatorChar == '\\'
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static Dictionary<string, CssStylesheet> CreateIndex(
        IReadOnlyList<CssStylesheet> stylesheets) {
        var sheetsByPath = new Dictionary<string, CssStylesheet>(Comparer);
        foreach (var stylesheet in stylesheets.OrderBy(
                     static sheet => sheet.Path,
                     StringComparer.Ordinal)) {
            var path = Normalize(stylesheet.Path);
            if (!sheetsByPath.ContainsKey(path)) {
                sheetsByPath.Add(path, stylesheet);
            }
        }

        return sheetsByPath;
    }

    public static string Normalize(string path) {
        try {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        } catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException) {
            return path;
        }
    }
}
