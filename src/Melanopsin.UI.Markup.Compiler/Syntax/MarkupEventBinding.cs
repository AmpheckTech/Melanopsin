using System;
using System.Text;

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal static class MarkupEventBinding {
    /// <summary>
    /// The lambda parameter a handler body reads <c>$event</c> from. The name is a verbatim
    /// identifier and, more importantly, the same length as <c>$event</c>, so rewriting a body
    /// keeps every column where the author put it and the <c>#line</c> mapping survives.
    /// </summary>
    public const string EmittedParameterName = "@event";

    public static bool TryGetEventName(
        MarkupAttribute attribute,
        out string eventName) {
        if (attribute.Name.Length > 2 &&
            attribute.Name.StartsWith("on", StringComparison.Ordinal) &&
            char.IsUpper(attribute.Name[2])) {
            eventName = attribute.Name.Substring(2);
            return true;
        }

        eventName = string.Empty;
        return false;
    }

    public static bool TryGetStatement(
        MarkupAttribute attribute,
        out string statement) {
        if (attribute.Parts.Count == 1 &&
            attribute.Parts[0].Kind == MarkupValuePartKind.Literal) {
            statement = attribute.Parts[0].Value;
            return true;
        }

        statement = string.Empty;
        return false;
    }

    public static string RewriteEventArgument(
        string statement,
        out bool usesEventArgument) {
        usesEventArgument = false;
        var output = new StringBuilder(statement.Length);
        for (var index = 0; index < statement.Length;) {
            if (TrySkipNonCode(statement, ref index, output) ||
                TrySkipString(statement, ref index, output) ||
                TrySkipCharacter(statement, ref index, output)) {
                continue;
            }

            if (statement[index] == '$' &&
                index + 6 <= statement.Length &&
                string.CompareOrdinal(statement, index, "$event", 0, 6) == 0 &&
                (index + 6 == statement.Length ||
                 !IsIdentifierPart(statement[index + 6]))) {
                output.Append(EmittedParameterName);
                usesEventArgument = true;
                index += 6;
                continue;
            }

            output.Append(statement[index++]);
        }

        return output.ToString();
    }

    /// <summary>
    /// Whether generated text reads as the markup an author wrote. The two are identical except
    /// where a handler used <c>$event</c>, which the generated lambda spells
    /// <see cref="EmittedParameterName"/>.
    /// </summary>
    public static bool TextMatches(string emitted, string authored) {
        if (emitted.Length != authored.Length) {
            return false;
        }

        for (var index = 0; index < emitted.Length; index++) {
            if (emitted[index] == authored[index]) {
                continue;
            }

            if (index + 6 > authored.Length ||
                string.CompareOrdinal(emitted, index, EmittedParameterName, 0, 6) != 0 ||
                string.CompareOrdinal(authored, index, "$event", 0, 6) != 0) {
                return false;
            }

            index += 5;
        }

        return true;
    }

    private static bool TrySkipNonCode(
        string text,
        ref int index,
        StringBuilder output) {
        if (text[index] == '/' && index + 1 < text.Length) {
            if (text[index + 1] == '/') {
                var end = text.IndexOf('\n', index + 2);
                end = end < 0 ? text.Length : end + 1;
                output.Append(text, index, end - index);
                index = end;
                return true;
            }

            if (text[index + 1] == '*') {
                var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
                output.Append(text, index, end - index);
                index = end;
                return true;
            }
        }

        return false;
    }

    private static bool TrySkipString(
        string text,
        ref int index,
        StringBuilder output) {
        if (text[index] != '"') {
            return false;
        }

        var quoteCount = 1;
        while (index + quoteCount < text.Length &&
               text[index + quoteCount] == '"') {
            quoteCount++;
        }

        var start = index;
        if (quoteCount >= 3) {
            var delimiter = new string('"', quoteCount);
            var end = text.IndexOf(delimiter, index + quoteCount, StringComparison.Ordinal);
            index = end < 0 ? text.Length : end + quoteCount;
            output.Append(text, start, index - start);
            return true;
        }

        var verbatim = index > 0 &&
                       (text[index - 1] == '@' ||
                        index > 1 && text[index - 2] == '@' && text[index - 1] == '$');
        index++;
        while (index < text.Length) {
            if (verbatim && text[index] == '"') {
                if (index + 1 < text.Length && text[index + 1] == '"') {
                    index += 2;
                    continue;
                }

                index++;
                break;
            }

            if (!verbatim && text[index] == '\\') {
                index = Math.Min(text.Length, index + 2);
                continue;
            }

            if (!verbatim && text[index] == '"') {
                index++;
                break;
            }

            index++;
        }

        output.Append(text, start, index - start);
        return true;
    }

    private static bool TrySkipCharacter(
        string text,
        ref int index,
        StringBuilder output) {
        if (text[index] != '\'') {
            return false;
        }

        var start = index++;
        while (index < text.Length) {
            if (text[index] == '\\') {
                index = Math.Min(text.Length, index + 2);
                continue;
            }

            if (text[index++] == '\'') {
                break;
            }
        }

        output.Append(text, start, index - start);
        return true;
    }

    private static bool IsIdentifierPart(char value) {
        return char.IsLetterOrDigit(value) || value == '_' || value == '$';
    }
}
