using System;
using System.Globalization;
using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Styling;

internal static partial class CssLiteralParser {
    public static string? ParseLiteral(
        string source,
        StyleValueKind kind,
        int offset,
        System.Collections.Generic.List<CssDiagnostic> diagnostics) {
        if (TryParseLiteral(source, kind, out var expression, out var error)) {
            return expression;
        }

        diagnostics.Add(new CssDiagnostic(
            MarkupDiagnosticCodes.InvalidStylesheet,
            error ?? $"Invalid {CssValueSyntax.TokenTypeName(kind)} value '{source}'.",
            offset,
            Math.Max(1, source.Length)));
        return null;
    }

    public static bool TryParseLiteral(
        string source,
        StyleValueKind kind,
        out string? expression,
        out string? error) {
        var value = source.Trim();
        expression = null;
        error = null;
        switch (kind) {
            case StyleValueKind.Color:
                return CssColorParser.TryParse(value, out expression, out error);
            case StyleValueKind.Length:
                return TryParseLength(value, out expression, out error);
            case StyleValueKind.Number:
                if (TryParseFloat(value, out var number)) {
                    expression = FloatLiteral(number);
                    return true;
                }

                error = $"Invalid number '{value}'.";
                return false;
            case StyleValueKind.Integer:
                if (int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var integer)) {
                    expression = integer.ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                error = $"Invalid integer '{value}'.";
                return false;
            case StyleValueKind.Duration:
                return TryParseDuration(value, out expression, out error);
            case StyleValueKind.Easing:
                return TryParseEasing(value, out expression, out error);
            case StyleValueKind.LayoutAlignment:
                return TryParseLayoutAlignment(value, out expression);
            case StyleValueKind.Anchor:
                return TryParseAnchor(value, out expression);
            case StyleValueKind.TextAlignment:
                return TryParseTextAlignment(value, out expression);
            case StyleValueKind.VerticalAlignment:
                return TryParseVerticalAlignment(value, out expression);
            case StyleValueKind.Cursor:
                return TryParseCursor(value, out expression);
            case StyleValueKind.Blur:
                return TryParseBlur(value, out expression, out error);
            default:
                error = $"Unsupported value type '{kind}'.";
                return false;
        }
    }

    internal static bool TryGetFunctionArguments(
        string value,
        string functionName,
        out string arguments) {
        arguments = string.Empty;
        if (value.Length <= functionName.Length + 1 ||
            value[value.Length - 1] != ')' ||
            value[functionName.Length] != '(' ||
            !value.Substring(0, functionName.Length).Equals(
                functionName,
                StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        arguments = value.Substring(
            functionName.Length + 1,
            value.Length - functionName.Length - 2);
        return true;
    }

    internal static bool TryParseFiniteDouble(string value, out double result) {
        return double.TryParse(
                   value.Trim(),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out result) &&
               !double.IsNaN(result) &&
               !double.IsInfinity(result);
    }

}
