// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text;

namespace Melanopsin.UI.Markup.Compiler.Generation.Formatting;

internal static class CSharpLiteral {
    public static string Quote(string value) {
        var output = new StringBuilder(value.Length + 2);
        output.Append('"');

        foreach (var character in value) {
            switch (character) {
                case '\\':
                    output.Append("\\\\");
                    break;
                case '"':
                    output.Append("\\\"");
                    break;
                case '\r':
                    output.Append("\\r");
                    break;
                case '\n':
                    output.Append("\\n");
                    break;
                case '\t':
                    output.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character)) {
                        output.Append("\\u")
                            .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    } else {
                        output.Append(character);
                    }

                    break;
            }
        }

        output.Append('"');
        return output.ToString();
    }
}
