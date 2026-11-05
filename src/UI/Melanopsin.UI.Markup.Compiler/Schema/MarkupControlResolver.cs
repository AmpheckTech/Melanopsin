// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed record MarkupControlResolution(
    ControlDefinition? Control,
    MarkupDiagnostic? Diagnostic);

internal sealed class MarkupControlResolver {
    private const string ClrNamespacePrefix = "clr-namespace:";
    private const string AssemblyPrefix = "assembly=";
    private readonly ControlCatalog _controls;
    private readonly MarkupDocument _document;

    public MarkupControlResolver(MarkupDocument document, ControlCatalog controls) {
        _document = document;
        _controls = controls;
    }

    public ControlDefinition? Find(MarkupElement element) {
        return Resolve(element).Control;
    }

    public MarkupControlResolution Resolve(MarkupElement element) {
        return element.NamespaceUri.Length == 0
            ? ResolveUnqualified(element)
            : ResolveQualified(element);
    }

    private MarkupControlResolution ResolveUnqualified(MarkupElement element) {
        if (_document.Namespace is { Length: > 0 } package) {
            var packaged = _controls.Find(package, element.Name);
            if (packaged is not null) {
                return Success(packaged);
            }
        }

        var candidates = _controls.FindAll(element.Name);
        if (candidates.Count == 1) {
            return Success(candidates[0]);
        }

        if (candidates.Count > 1) {
            var qualifiedNames = string.Join(
                ", ",
                candidates
                    .Select(static candidate => $"'{candidate.QualifiedName}'")
                    .OrderBy(static name => name, StringComparer.Ordinal));
            return Failure(
                MarkupDiagnosticCodes.AmbiguousControl,
                $"Unqualified control '{element.Name}' is ambiguous. It is defined in " +
                $"{qualifiedNames}. Qualify the element with a clr-namespace XML prefix.",
                element,
                element.QualifiedName.Length + 1);
        }

        var packageHint = _document.Namespace is { Length: > 0 } namespaceName
            ? $" package '{namespaceName}'"
            : string.Empty;
        return Failure(
            MarkupDiagnosticCodes.UnknownControl,
            $"Unknown control '{element.QualifiedName}'. No markup element with that name " +
            $"is registered in{packageHint} or as a unique global name.",
            element,
            element.QualifiedName.Length + 1);
    }

    private MarkupControlResolution ResolveQualified(MarkupElement element) {
        if (!_document.NamespaceUris.Contains(element.NamespaceUri)) {
            return Failure(
                MarkupDiagnosticCodes.InvalidNamespace,
                $"XML namespace '{element.NamespaceUri}' does not have a CLR namespace mapping.",
                element,
                element.QualifiedName.Length + 1);
        }

        if (!TryParseNamespace(
                element.NamespaceUri,
                out var namespaceName,
                out var assemblyName,
                out var error)) {
            return Failure(
                MarkupDiagnosticCodes.InvalidNamespace,
                error,
                element,
                element.QualifiedName.Length + 1);
        }

        var definition = _controls.Find(namespaceName, assemblyName, element.Name);
        if (definition is not null) {
            return Success(definition);
        }

        return Failure(
            MarkupDiagnosticCodes.UnknownControlNamespace,
            $"No markup element '{element.Name}' is defined in CLR namespace " +
            $"'{namespaceName}' from assembly '{assemblyName}'.",
            element,
            element.QualifiedName.Length + 1);
    }

    private bool TryParseNamespace(
        string namespaceUri,
        out string namespaceName,
        out string assemblyName,
        out string error) {
        namespaceName = string.Empty;
        assemblyName = _controls.AssemblyName;
        error = string.Empty;
        if (!namespaceUri.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal)) {
            error = $"XML namespace '{namespaceUri}' is not a CLR namespace mapping. " +
                    "Use clr-namespace:Namespace;assembly=Assembly.";
            return false;
        }

        var body = namespaceUri.Substring(ClrNamespacePrefix.Length);
        var separator = body.IndexOf(';');
        namespaceName = separator < 0 ? body : body.Substring(0, separator);
        if (!IsValidNamespaceName(namespaceName)) {
            error = $"XML namespace '{namespaceUri}' has an invalid CLR namespace.";
            return false;
        }

        if (separator < 0) {
            return true;
        }

        var assemblyClause = body.Substring(separator + 1);
        if (!assemblyClause.StartsWith(AssemblyPrefix, StringComparison.Ordinal) ||
            assemblyClause.Length == AssemblyPrefix.Length) {
            error = $"XML namespace '{namespaceUri}' has an invalid assembly clause. " +
                    "Use ;assembly=Assembly.";
            return false;
        }

        assemblyName = assemblyClause.Substring(AssemblyPrefix.Length);
        if (assemblyName.Contains(';')) {
            error = $"XML namespace '{namespaceUri}' contains more than one assembly clause.";
            return false;
        }

        return true;
    }

    private static bool IsValidNamespaceName(string value) {
        if (value.Length == 0 || value.Trim().Length != value.Length) {
            return false;
        }

        foreach (var part in value.Split('.')) {
            var identifier = part.Length > 0 && part[0] == '@'
                ? part.Substring(1)
                : part;
            if (!SyntaxFacts.IsValidIdentifier(identifier)) {
                return false;
            }
        }

        return true;
    }

    private static MarkupControlResolution Success(ControlDefinition definition) {
        return new MarkupControlResolution(definition, null);
    }

    private static MarkupControlResolution Failure(
        string code,
        string message,
        MarkupElement element,
        int length) {
        return new MarkupControlResolution(
            null,
            new MarkupDiagnostic(code, message, element.Offset, Math.Max(1, length)));
    }
}
