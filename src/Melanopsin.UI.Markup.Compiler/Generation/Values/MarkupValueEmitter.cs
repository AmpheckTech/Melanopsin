using System;
using System.Collections.Generic;
using Melanopsin.UI.Markup.Compiler.Generation.Formatting;
using Melanopsin.UI.Markup.Compiler.Schema;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Generation.Values;

internal static class MarkupValueEmitter {
    public static string AttributeValue(IReadOnlyList<MarkupValuePart> parts) {
        ThrowIfResource(parts);
        if (parts.Count == 1) {
            var part = parts[0];
            return part.IsExpression
                ? Parenthesize(part.Value)
                : CSharpLiteral.Quote(part.Value);
        }

        var values = new List<string>(parts.Count);
        foreach (var part in parts) {
            values.Add(part.IsExpression
                ? ToStringExpression(part.Value)
                : CSharpLiteral.Quote(part.Value));
        }

        return $"global::System.String.Concat({string.Join(", ", values)})";
    }

    public static string PropertyValue(
        IReadOnlyList<MarkupValuePart> parts,
        MarkupAttributeDefinition definition) {
        ThrowIfResource(parts);
        var typeName = definition.PropertyTypeName ?? "object";
        if (parts.Count > 1) {
            return ToStringExpression(AttributeValue(parts));
        }

        var part = parts[0];
        if (part.IsExpression) {
            return IsStringType(typeName)
                ? ToStringExpression(part.Value)
                : Parenthesize(part.Value);
        }

        if (definition.ValueKind == SchemaValueKind.Enum &&
            definition.EnumMembers.TryGetValue(part.Value.ToLowerInvariant(), out var member)) {
            return $"{typeName}.{member}";
        }

        if (definition.ValueKind == SchemaValueKind.Boolean) {
            return part.Value.ToLowerInvariant();
        }

        if (definition.ValueKind == SchemaValueKind.Input) {
            return string.Equals(part.Value, "none", StringComparison.OrdinalIgnoreCase)
                ? "false"
                : "true";
        }

        if (definition.ValueKind == SchemaValueKind.Number) {
            return typeName switch {
                "float" or "float?" => part.Value + "f",
                "double" or "double?" => part.Value + "d",
                "decimal" or "decimal?" => part.Value + "m",
                _ => part.Value,
            };
        }

        if (definition.ValueKind == SchemaValueKind.Integer) {
            return part.Value;
        }

        return CSharpLiteral.Quote(part.Value);
    }

    public static string BoundPropertyValue(
        string valueExpression,
        int partCount,
        MarkupAttributeDefinition definition) {
        var typeName = definition.PropertyTypeName ?? "object";
        return partCount > 1 || IsStringType(typeName)
            ? ToStringExpression(valueExpression)
            : Parenthesize(valueExpression);
    }

    public static string LengthExpression(IReadOnlyList<MarkupValuePart> parts) {
        ThrowIfResource(parts);
        if (parts.Count != 1) {
            throw new InvalidOperationException("A length must contain exactly one value.");
        }

        var part = parts[0];
        if (!part.IsExpression) {
            return LengthLiteral(part.Value);
        }

        return "global::Melanopsin.UI.Length.Parse(" +
               "global::System.Convert.ToString(" +
               Parenthesize(part.Value) +
               ", global::System.Globalization.CultureInfo.InvariantCulture) ??" +
               " global::System.String.Empty)";
    }

    public static string ToStringExpression(string expression) {
        return "global::System.Convert.ToString(" +
               Parenthesize(expression) +
               ", global::System.Globalization.CultureInfo.InvariantCulture) ?? global::System.String.Empty";
    }

    public static string Parenthesize(string expression) {
        return "(" + expression + ")";
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

        var factory = "Pixels";
        var units = new[] {
            ("su", "Space"),
            ("ru", "Radius"),
            ("fu", "Font"),
            ("iu", "Icon"),
            ("hx", "Hairline"),
            ("vw", "ViewportWidth"),
            ("vh", "ViewportHeight"),
            ("fr", "Fraction"),
            ("rem", "Rem"),
            ("em", "Em"),
            ("dp", "DeviceIndependent"),
            ("sp", "ScaleIndependent"),
            ("pt", "Point"),
            ("px", "Pixels"),
            ("%", "Percent"),
        };
        foreach (var (suffix, name) in units) {
            if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            factory = name;
            normalized = normalized.Substring(0, normalized.Length - suffix.Length);
            break;
        }

        if (factory == "Pixels" &&
            normalized.EndsWith("px", StringComparison.OrdinalIgnoreCase)) {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }

        return "global::Melanopsin.UI.Length." +
               factory +
               "(" +
               normalized +
               "f)";
    }
}
