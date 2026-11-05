using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Generation.Formatting;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation.Values;

/// <summary>
/// Turns the parts of an attribute value into the C# that computes it. Literals become the constant
/// the schema asks for and interpolations keep the offset they had in the document, so the code they
/// turn into maps back onto the expression the author wrote.
/// </summary>
internal static class MarkupValueEmitter {
    public static CodeFragment AttributeValue(IReadOnlyList<MarkupValuePart> parts) {
        ThrowIfResource(parts);
        if (parts.Count == 1) {
            return parts[0].IsExpression
                ? CodeFragment.Parenthesize(Expression(parts[0]))
                : CodeFragment.Text(CSharpLiteral.Quote(parts[0].Value));
        }

        var values = CodeFragment.Text("global::System.String.Concat(");
        for (var index = 0; index < parts.Count; index++) {
            if (index != 0) {
                values.Add(", ");
            }

            var part = parts[index];
            values.Add(part.IsExpression
                ? ToStringExpression(Expression(part))
                : CodeFragment.Text(CSharpLiteral.Quote(part.Value)));
        }

        return values.Add(")");
    }

    public static CodeFragment AttributeValue(
        IReadOnlyList<MarkupValuePart> parts,
        MarkupAttributeDefinition definition) {
        return definition.ValueKind == SchemaValueKind.Length
            ? LengthExpression(parts)
            : PropertyValue(parts, definition);
    }

    public static CodeFragment PropertyValue(
        IReadOnlyList<MarkupValuePart> parts,
        MarkupAttributeDefinition definition) {
        ThrowIfResource(parts);
        var typeName = definition.ValueTypeName ?? "object";
        if (parts.Count > 1) {
            return ToStringExpression(AttributeValue(parts));
        }

        var part = parts[0];
        if (part.IsExpression) {
            return IsStringType(typeName)
                ? ToStringExpression(Expression(part))
                : CodeFragment.Parenthesize(Expression(part));
        }

        if (definition.ValueKind == SchemaValueKind.Enum &&
            definition.EnumMembers.TryGetValue(part.Value.ToLowerInvariant(), out var member)) {
            return CodeFragment.Text($"{typeName}.{member}");
        }

        if (definition.ValueKind == SchemaValueKind.Boolean) {
            return CodeFragment.Text(part.Value.ToLowerInvariant());
        }

        if (definition.ValueKind == SchemaValueKind.Input) {
            return CodeFragment.Text(string.Equals(
                part.Value,
                "none",
                StringComparison.OrdinalIgnoreCase)
                ? "false"
                : "true");
        }

        if (definition.ValueKind == SchemaValueKind.Number) {
            return CodeFragment.Text(typeName switch {
                "float" or "float?" => part.Value + "f",
                "double" or "double?" => part.Value + "d",
                "decimal" or "decimal?" => part.Value + "m",
                _ => part.Value,
            });
        }

        if (definition.ValueKind == SchemaValueKind.Integer) {
            return CodeFragment.Text(part.Value);
        }

        if (definition.ValueKind == SchemaValueKind.Color) {
            return CodeFragment.Text(ColorLiteral(part.Value));
        }

        return CodeFragment.Text(CSharpLiteral.Quote(part.Value));
    }

    public static CodeFragment LengthExpression(IReadOnlyList<MarkupValuePart> parts) {
        ThrowIfResource(parts);
        if (parts.Count == 1 && !parts[0].IsExpression) {
            return CodeFragment.Text(LengthLiteral(parts[0].Value));
        }

        if (TryGetUnitExpression(parts, out var part, out var factory)) {
            var expression = CodeFragment.Text($"global::Melanopsin.UI.Length.{factory}(")
                .Add("global::System.Convert.ToSingle(")
                .Add(CodeFragment.Parenthesize(Expression(part!)))
                .Add(", global::System.Globalization.CultureInfo.InvariantCulture))");
            return expression;
        }

        if (parts.Count == 1 && parts[0].IsExpression) {
            return CodeFragment.Parenthesize(Expression(parts[0]));
        }

        throw new InvalidOperationException(
            "A length expression must be a literal, a typed expression, or a single expression with a static unit suffix.");
    }

    /// <summary>The expression of an interpolation, and the markup it came from.</summary>
    public static CodeFragment Expression(MarkupValuePart part) {
        return CodeFragment.Source(part.Value, part.Offset);
    }

    public static CodeFragment ToStringExpression(CodeFragment expression) {
        return CodeFragment.Text("global::System.Convert.ToString(")
            .Add(CodeFragment.Parenthesize(expression))
            .Add(", global::System.Globalization.CultureInfo.InvariantCulture) " +
                 "?? global::System.String.Empty");
    }

    private static void ThrowIfResource(IReadOnlyList<MarkupValuePart> parts) {
        foreach (var part in parts) {
            if (part.IsResource) {
                throw new InvalidOperationException(
                    "A resource reference must be emitted as a reactive resource binding.");
            }
        }
    }

    private static bool IsStringType(string typeName) {
        return typeName is "string" or "global::System.String";
    }

    private static string LengthLiteral(string value) {
        var normalized = value.Trim();
        if (string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase)) {
            return "global::Melanopsin.UI.Length.Auto";
        }

        var factory = "Px";
        foreach (var (suffix, name) in LengthUnitSyntax.Units) {
            if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            factory = name;
            normalized = normalized.Substring(0, normalized.Length - suffix.Length);
            break;
        }

        return "global::Melanopsin.UI.Length." +
               factory +
               "(" +
               normalized +
               "f)";
    }

    private static string ColorLiteral(string value) {
        var normalized = value.Trim();
        if (string.Equals(normalized, "transparent", StringComparison.OrdinalIgnoreCase)) {
            return "new global::Melanopsin.Graphics.Color(0, 0, 0, 0)";
        }

        if (normalized.Length == 7 && normalized[0] == '#' &&
            byte.TryParse(normalized.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) &&
            byte.TryParse(normalized.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) &&
            byte.TryParse(normalized.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue)) {
            return $"new global::Melanopsin.Graphics.Color({red}, {green}, {blue})";
        }

        throw new InvalidOperationException($"Invalid color literal '{value}'.");
    }

    private static bool TryGetUnitExpression(
        IReadOnlyList<MarkupValuePart> parts,
        out MarkupValuePart? expression,
        out string factory) {
        MarkupValuePart? found = null;
        foreach (var part in parts) {
            if (!part.IsExpression) {
                continue;
            }

            if (found is not null) {
                expression = null;
                factory = string.Empty;
                return false;
            }

            found = part;
        }

        if (found is null) {
            expression = null;
            factory = string.Empty;
            return false;
        }

        var suffix = string.Concat(
            parts.Where(static part => !part.IsExpression)
                .Select(static part => part.Value))
            .Trim();
        var match = LengthUnitSyntax.Units.FirstOrDefault(
            unit => string.Equals(
                unit.Suffix,
                suffix,
                StringComparison.OrdinalIgnoreCase)).Factory;
        if (match is null) {
            expression = null;
            factory = string.Empty;
            return false;
        }

        expression = found;
        factory = match;
        return true;
    }
}
