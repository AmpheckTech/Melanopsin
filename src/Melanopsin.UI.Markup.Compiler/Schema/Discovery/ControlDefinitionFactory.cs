using System;
using System.Collections.Generic;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Schema.Naming;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Schema.Discovery;

internal static class ControlDefinitionFactory {
    public static ControlDefinition? Create(
        Compilation compilation,
        INamedTypeSymbol type,
        List<MarkupSchemaDiagnostic> diagnostics) {
        var markupElement = type.GetAttributes().First(attribute =>
            attribute.AttributeClass?.ToDisplayString() == MarkupMetadataNames.MarkupElementAttribute);
        var declaredName = markupElement.ConstructorArguments.Length == 0
            ? type.Name
            : markupElement.ConstructorArguments[0].Value as string;
        var name = string.IsNullOrWhiteSpace(declaredName) ? type.Name : declaredName!;

        if (type.IsAbstract || type.TypeKind != TypeKind.Class || type.Arity != 0) {
            AddSchemaError(
                diagnostics,
                $"Markup element '{name}' must be a non-abstract, non-generic class.");
            return null;
        }

        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) {
            AddSchemaError(
                diagnostics,
                $"Markup element '{name}' must be accessible from generated code.");
            return null;
        }

        var constructor = type.InstanceConstructors.FirstOrDefault(static candidate =>
            candidate.Parameters.Length == 0);
        if (constructor is null ||
            !compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly)) {
            AddSchemaError(
                diagnostics,
                $"Markup element '{name}' must have an accessible parameterless constructor.");
            return null;
        }

        var attributes = new Dictionary<string, MarkupAttributeDefinition>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType) {
            foreach (var attribute in current.GetAttributes()) {
                AddAttribute(
                    compilation,
                    attributes,
                    diagnostics,
                    name,
                    attribute,
                    property: null);
            }

            foreach (var property in current.GetMembers().OfType<IPropertySymbol>()) {
                foreach (var attribute in property.GetAttributes()) {
                    AddAttribute(
                        compilation,
                        attributes,
                        diagnostics,
                        name,
                        attribute,
                        property);
                }
            }
        }

        return new ControlDefinition(
            name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            attributes);
    }

    private static void AddAttribute(
        Compilation compilation,
        Dictionary<string, MarkupAttributeDefinition> attributes,
        List<MarkupSchemaDiagnostic> diagnostics,
        string controlName,
        AttributeData attribute,
        IPropertySymbol? property) {
        if (attribute.AttributeClass?.ToDisplayString() != MarkupMetadataNames.MarkupAttributeAttribute ||
            attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Value is not string name) {
            return;
        }

        var valueKind = ReadValueKind(attribute);
        var enumMembers = valueKind == SchemaValueKind.Enum
            ? ReadEnumMembers(property?.Type)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var allowedValues = enumMembers.Keys.ToArray();
        var valueTypeName = property?.Type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
        var canAssignProperty =
            property?.SetMethod is { } setter &&
            compilation.IsSymbolAccessibleWithin(setter, compilation.Assembly);
        var definition = new MarkupAttributeDefinition(
            name,
            valueKind,
            allowedValues,
            valueTypeName,
            canAssignProperty ? property?.Name : null,
            canAssignProperty ? valueTypeName : null,
            enumMembers);

        if (attributes.TryGetValue(name, out var existing)) {
            if (existing.ValueKind != valueKind ||
                !existing.AllowedValues.SequenceEqual(allowedValues, StringComparer.Ordinal)) {
                AddSchemaError(
                    diagnostics,
                    $"Markup attribute '{controlName}.{name}' has conflicting declarations.");
            }

            return;
        }

        attributes.Add(name, definition);
    }

    private static SchemaValueKind ReadValueKind(AttributeData attribute) {
        if (attribute.ConstructorArguments.Length < 2 ||
            attribute.ConstructorArguments[1].Value is not int value) {
            return SchemaValueKind.Any;
        }

        var enumType = attribute.ConstructorArguments[1].Type as INamedTypeSymbol;
        var member = enumType?.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(candidate =>
                candidate.HasConstantValue &&
                candidate.ConstantValue is int constant &&
                constant == value);

        return member?.Name switch {
            "Number" => SchemaValueKind.Number,
            "Length" => SchemaValueKind.Length,
            "Integer" => SchemaValueKind.Integer,
            "Boolean" => SchemaValueKind.Boolean,
            "Enum" => SchemaValueKind.Enum,
            "Input" => SchemaValueKind.Input,
            _ => SchemaValueKind.Any,
        };
    }

    private static IReadOnlyDictionary<string, string> ReadEnumMembers(ITypeSymbol? propertyType) {
        if (propertyType?.TypeKind != TypeKind.Enum) {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return propertyType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue)
            .ToDictionary(
                static field => MarkupNameNormalizer.ToKebabCase(field.Name),
                static field => field.Name,
                StringComparer.Ordinal);
    }

    private static void AddSchemaError(
        List<MarkupSchemaDiagnostic> diagnostics,
        string message) {
        diagnostics.Add(new MarkupSchemaDiagnostic(
            MarkupDiagnosticCodes.InvalidControlSchema,
            message));
    }
}
