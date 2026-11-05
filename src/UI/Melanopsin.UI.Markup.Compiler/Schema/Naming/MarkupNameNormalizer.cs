using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Schema.Naming;

internal static class MarkupNameNormalizer {
    public static string ToKebabCase(string name) {
        var characters = new List<char>(name.Length + 4);
        for (var index = 0; index < name.Length; index++) {
            var character = name[index];
            if (char.IsUpper(character) &&
                index > 0 &&
                (char.IsLower(name[index - 1]) ||
                 (index + 1 < name.Length && char.IsLower(name[index + 1])))) {
                characters.Add('-');
            }

            characters.Add(char.ToLowerInvariant(character));
        }

        return new string(characters.ToArray());
    }
}
