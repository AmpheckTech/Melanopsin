// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

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
        var markupAttribute = type.GetAttributes().First(attribute =>
            attribute.AttributeClass?.ToDisplayString() is
                MarkupMetadataNames.MarkupElementAttribute or
                MarkupMetadataNames.MarkupComponentAttribute);
        var isMarkupComponent = markupAttribute.AttributeClass?.ToDisplayString() ==
            MarkupMetadataNames.MarkupComponentAttribute;
        var componentBase = compilation.GetTypeByMetadataName(MarkupMetadataNames.Component);
        var isComponent = componentBase is not null && DerivesFrom(type, componentBase);
        var declaredName = markupAttribute.ConstructorArguments.Length == 0
            ? type.Name
            : markupAttribute.ConstructorArguments[0].Value as string;
        var name = string.IsNullOrWhiteSpace(declaredName) ? type.Name : declaredName!;

        if (isMarkupComponent != isComponent) {
            AddSchemaError(
                diagnostics,
                isMarkupComponent
                    ? $"Markup component '{name}' must derive from Melanopsin.UI.Component."
                    : $"Component '{name}' must use MarkupComponentAttribute, not " +
                      "MarkupElementAttribute.");
            return null;
        }

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

        var schemaType = isComponent
            ? compilation.GetTypeByMetadataName(MarkupMetadataNames.ComponentHost) ??
              throw new InvalidOperationException("ComponentHost is not referenced.")
            : type;
        var attributes = new Dictionary<string, MarkupAttributeDefinition>(StringComparer.Ordinal);
        var events = new Dictionary<string, ControlEventDefinition>(StringComparer.Ordinal);
        for (var current = schemaType; current is not null; current = current.BaseType) {
            foreach (var attribute in current.GetAttributes()) {
                AddAttribute(
                    compilation,
                    attributes,
                    diagnostics,
                    name,
                    attribute,
                    property: null,
                    declaringType: current);
            }

            foreach (var property in current.GetMembers().OfType<IPropertySymbol>()) {
                foreach (var attribute in property.GetAttributes()) {
                    AddAttribute(
                        compilation,
                        attributes,
                        diagnostics,
                        name,
                        attribute,
                    property,
                    declaringType: current);
                }
            }

            foreach (var field in current.GetMembers().OfType<IFieldSymbol>()) {
                foreach (var attribute in field.GetAttributes()) {
                    AddAttribute(
                        compilation,
                        attributes,
                        diagnostics,
                        name,
                        attribute,
                        property: null,
                    field,
                    declaringType: current);
                }

                AddDescriptor(
                    compilation,
                    attributes,
                    diagnostics,
                    name,
                    field);
            }

            foreach (var eventSymbol in current.GetMembers().OfType<IEventSymbol>()) {
                AddEvent(compilation, events, eventSymbol);
            }
        }

        return new ControlDefinition(
            name,
            type.ContainingNamespace.ToDisplayString(),
            type.ContainingAssembly.Name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            attributes,
            events,
            isComponent);
    }

    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType) {
        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) {
                return true;
            }
        }

        return false;
    }

    private static void AddEvent(
        Compilation compilation,
        Dictionary<string, ControlEventDefinition> events,
        IEventSymbol eventSymbol) {
        if (eventSymbol.IsStatic ||
            !compilation.IsSymbolAccessibleWithin(eventSymbol, compilation.Assembly) ||
            eventSymbol.Type is not INamedTypeSymbol delegateType ||
            delegateType.DelegateInvokeMethod is not { } invokeMethod) {
            return;
        }

        var parameters = invokeMethod.Parameters;
        if (!events.ContainsKey(eventSymbol.Name)) {
            events.Add(eventSymbol.Name, new ControlEventDefinition(
                eventSymbol.Name,
                eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                parameters.Length,
                parameters.Length == 1
                    ? parameters[0].Type.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat)
                    : null));
        }
    }

    private static void AddAttribute(
        Compilation compilation,
        Dictionary<string, MarkupAttributeDefinition> attributes,
        List<MarkupSchemaDiagnostic> diagnostics,
        string controlName,
        AttributeData attribute,
        IPropertySymbol? property,
        IFieldSymbol? field = null,
        INamedTypeSymbol? declaringType = null) {
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
        var declaredType = property?.Type ?? GetDescriptorValueType(field);
        var valueTypeName = declaredType?.ToDisplayString(
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
            enumMembers,
            DeclaredPropertyName: property?.Name ?? field?.Name,
            DeclaringTypeName: declaringType?.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat));

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

    private static void AddDescriptor(
        Compilation compilation,
        Dictionary<string, MarkupAttributeDefinition> attributes,
        List<MarkupSchemaDiagnostic> diagnostics,
        string controlName,
        IFieldSymbol field) {
        if (!field.IsStatic ||
            field.DeclaredAccessibility == Accessibility.Private ||
            !compilation.IsSymbolAccessibleWithin(field, compilation.Assembly) ||
            field.Type is not INamedTypeSymbol descriptorType ||
            descriptorType.Name != MarkupMetadataNames.ElementAttribute ||
            descriptorType.ContainingNamespace.ToDisplayString() != "Melanopsin.UI" ||
            descriptorType.TypeArguments.Length != 1) {
            return;
        }

        var markupAttribute = field.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
                MarkupMetadataNames.MarkupAttributeAttribute);
        if (markupAttribute is null ||
            markupAttribute.ConstructorArguments.Length == 0 ||
            markupAttribute.ConstructorArguments[0].Value is not string attributeName) {
            return;
        }

        var valueKind = ReadValueKind(markupAttribute);
        var valueType = descriptorType.TypeArguments[0];
        var valueTypeName = valueType.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
        var enumMembers = valueKind == SchemaValueKind.Enum
            ? ReadEnumMembers(valueType)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var definition = new MarkupAttributeDefinition(
            attributeName,
            valueKind,
            enumMembers.Keys.ToArray(),
            valueTypeName,
            null,
            null,
            enumMembers,
            field.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) +
            "." +
            field.Name);

        if (attributes.TryGetValue(attributeName, out var existing)) {
            if (existing.ValueKind != valueKind ||
                (existing.ValueTypeName is not null &&
                 !string.Equals(
                     existing.ValueTypeName,
                     valueTypeName,
                     StringComparison.Ordinal))) {
                AddSchemaError(
                    diagnostics,
                    $"Markup attribute '{controlName}.{attributeName}' has conflicting declarations.");
                return;
            }

            if (existing.DescriptorAccessor is not null &&
                !string.Equals(
                    existing.DescriptorAccessor,
                    definition.DescriptorAccessor,
                    StringComparison.Ordinal)) {
                AddSchemaError(
                    diagnostics,
                    $"Markup attribute '{controlName}.{attributeName}' has multiple " +
                    "element attribute descriptors.");
                return;
            }

            attributes[attributeName] = existing with {
                AllowedValues = existing.AllowedValues.Count == 0
                    ? definition.AllowedValues
                    : existing.AllowedValues,
                ValueTypeName = existing.ValueTypeName ?? valueTypeName,
                EnumMembers = existing.EnumMembers.Count == 0
                    ? definition.EnumMembers
                    : existing.EnumMembers,
                DescriptorAccessor = existing.DescriptorAccessor ??
                    definition.DescriptorAccessor,
                DeclaredPropertyName = existing.DeclaredPropertyName ??
                    definition.DeclaredPropertyName,
                DeclaringTypeName = existing.DeclaringTypeName ??
                    definition.DeclaringTypeName,
            };
            return;
        }

        attributes.Add(attributeName, definition);
    }

    private static ITypeSymbol? GetDescriptorValueType(IFieldSymbol? field) {
        var fieldType = field?.Type as INamedTypeSymbol;
        return fieldType?.Name == MarkupMetadataNames.ElementAttribute &&
            fieldType.TypeArguments.Length == 1
                ? fieldType.TypeArguments[0]
                : null;
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
            "Color" => SchemaValueKind.Color,
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
