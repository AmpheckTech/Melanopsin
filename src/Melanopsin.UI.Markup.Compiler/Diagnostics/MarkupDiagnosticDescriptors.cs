using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Diagnostics;

internal static class MarkupDiagnosticDescriptors {
    private const string Category = "Melanopsin.UI.Markup.Compiler";

    public static readonly DiagnosticDescriptor InvalidSyntax = Create(
        MarkupDiagnosticCodes.InvalidSyntax,
        "Invalid XML view");

    public static readonly DiagnosticDescriptor UnsupportedDirective = Create(
        MarkupDiagnosticCodes.UnsupportedDirective,
        "Unsupported XML view directive");

    public static readonly DiagnosticDescriptor InvalidMetadata = Create(
        MarkupDiagnosticCodes.InvalidMetadata,
        "Invalid XML view metadata");

    public static readonly DiagnosticDescriptor InvalidExpression = Create(
        MarkupDiagnosticCodes.InvalidExpression,
        "Invalid XML view expression");

    public static readonly DiagnosticDescriptor DuplicateAttribute = Create(
        MarkupDiagnosticCodes.DuplicateAttribute,
        "Duplicate XML view attribute");

    public static readonly DiagnosticDescriptor UnknownControl = Create(
        MarkupDiagnosticCodes.UnknownControl,
        "Unknown XML view control");

    public static readonly DiagnosticDescriptor UnknownAttribute = Create(
        MarkupDiagnosticCodes.UnknownAttribute,
        "Unknown XML view control attribute");

    public static readonly DiagnosticDescriptor InvalidAttributeValue = Create(
        MarkupDiagnosticCodes.InvalidAttributeValue,
        "Invalid XML view control attribute value");

    public static readonly DiagnosticDescriptor InvalidControlSchema = Create(
        MarkupDiagnosticCodes.InvalidControlSchema,
        "Invalid markup control schema");

    public static readonly DiagnosticDescriptor InvalidComponentClass = Create(
        MarkupDiagnosticCodes.InvalidComponentClass,
        "Invalid markup component class");

    public static readonly DiagnosticDescriptor InvalidResourceManifest = Create(
        MarkupDiagnosticCodes.InvalidResourceManifest,
        "Invalid resource manifest");

    public static readonly DiagnosticDescriptor UnknownResource = Create(
        MarkupDiagnosticCodes.UnknownResource,
        "Unknown resource");

    public static readonly DiagnosticDescriptor InvalidResourceReference = Create(
        MarkupDiagnosticCodes.InvalidResourceReference,
        "Invalid resource reference");

    public static readonly DiagnosticDescriptor IncompatibleResourceType = Create(
        MarkupDiagnosticCodes.IncompatibleResourceType,
        "Incompatible resource type");

    public static readonly DiagnosticDescriptor InvalidReference = Create(
        MarkupDiagnosticCodes.InvalidReference,
        "Invalid markup reference");

    public static readonly DiagnosticDescriptor UnknownEvent = Create(
        MarkupDiagnosticCodes.UnknownEvent,
        "Unknown XML view control event");

    public static readonly DiagnosticDescriptor InvalidEventExpression = Create(
        MarkupDiagnosticCodes.InvalidEventExpression,
        "Invalid XML view event expression");

    public static readonly DiagnosticDescriptor InvalidStylesheet = Create(
        MarkupDiagnosticCodes.InvalidStylesheet,
        "Invalid typed stylesheet");

    public static readonly DiagnosticDescriptor UnknownStyleSlot = Create(
        MarkupDiagnosticCodes.UnknownStyleSlot,
        "Unknown style slot");

    public static readonly DiagnosticDescriptor StyleSlotTypeMismatch = Create(
        MarkupDiagnosticCodes.StyleSlotTypeMismatch,
        "Incompatible style slot type");

    public static readonly DiagnosticDescriptor InvalidNamespace = Create(
        MarkupDiagnosticCodes.InvalidNamespace,
        "Invalid CLR namespace mapping");

    public static readonly DiagnosticDescriptor AmbiguousControl = Create(
        MarkupDiagnosticCodes.AmbiguousControl,
        "Ambiguous unqualified XML view control");

    public static readonly DiagnosticDescriptor UnknownControlNamespace = Create(
        MarkupDiagnosticCodes.UnknownControlNamespace,
        "Unknown XML view control namespace");

    public static DiagnosticDescriptor ForCode(string code) {
        return code switch {
            MarkupDiagnosticCodes.UnsupportedDirective => UnsupportedDirective,
            MarkupDiagnosticCodes.InvalidMetadata => InvalidMetadata,
            MarkupDiagnosticCodes.InvalidExpression => InvalidExpression,
            MarkupDiagnosticCodes.DuplicateAttribute => DuplicateAttribute,
            MarkupDiagnosticCodes.UnknownControl => UnknownControl,
            MarkupDiagnosticCodes.UnknownAttribute => UnknownAttribute,
            MarkupDiagnosticCodes.InvalidAttributeValue => InvalidAttributeValue,
            MarkupDiagnosticCodes.InvalidControlSchema => InvalidControlSchema,
            MarkupDiagnosticCodes.InvalidComponentClass => InvalidComponentClass,
            MarkupDiagnosticCodes.InvalidResourceManifest => InvalidResourceManifest,
            MarkupDiagnosticCodes.UnknownResource => UnknownResource,
            MarkupDiagnosticCodes.InvalidResourceReference => InvalidResourceReference,
            MarkupDiagnosticCodes.IncompatibleResourceType => IncompatibleResourceType,
            MarkupDiagnosticCodes.InvalidReference => InvalidReference,
            MarkupDiagnosticCodes.UnknownEvent => UnknownEvent,
            MarkupDiagnosticCodes.InvalidEventExpression => InvalidEventExpression,
            MarkupDiagnosticCodes.InvalidStylesheet => InvalidStylesheet,
            MarkupDiagnosticCodes.UnknownStyleSlot => UnknownStyleSlot,
            MarkupDiagnosticCodes.StyleSlotTypeMismatch => StyleSlotTypeMismatch,
            MarkupDiagnosticCodes.InvalidNamespace => InvalidNamespace,
            MarkupDiagnosticCodes.AmbiguousControl => AmbiguousControl,
            MarkupDiagnosticCodes.UnknownControlNamespace => UnknownControlNamespace,
            _ => InvalidSyntax,
        };
    }

    private static DiagnosticDescriptor Create(string id, string title) {
        return new DiagnosticDescriptor(
            id,
            title,
            "{0}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
