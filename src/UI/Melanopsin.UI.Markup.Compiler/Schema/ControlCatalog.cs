using System;
using System.Collections.Generic;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Schema.Discovery;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed class ControlCatalog {
    private readonly Dictionary<string, ControlDefinition> _controls;
    private readonly Dictionary<string, List<ControlDefinition>> _controlsByName;

    private ControlCatalog(
        string assemblyName,
        Dictionary<string, ControlDefinition> controls,
        Dictionary<string, List<ControlDefinition>> controlsByName,
        IReadOnlyList<MarkupSchemaDiagnostic> diagnostics) {
        AssemblyName = assemblyName;
        _controls = controls;
        _controlsByName = controlsByName;
        Diagnostics = diagnostics;
    }

    public string AssemblyName { get; }

    public IReadOnlyList<MarkupSchemaDiagnostic> Diagnostics { get; }

    public IEnumerable<ControlDefinition> All => _controls.Values;

    public static ControlCatalog Create(
        Compilation compilation,
        CancellationToken cancellationToken) {
        var controls = new Dictionary<string, ControlDefinition>(StringComparer.Ordinal);
        var controlsByName = new Dictionary<string, List<ControlDefinition>>(
            StringComparer.Ordinal);
        var diagnostics = new List<MarkupSchemaDiagnostic>();

        foreach (var type in ControlTypeDiscovery.Enumerate(compilation, cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = ControlDefinitionFactory.Create(compilation, type, diagnostics);
            if (definition is null) {
                continue;
            }

            var key = Key(definition.Namespace, definition.Name);
            if (controls.TryGetValue(key, out var existing)) {
                var declaration = string.Equals(
                    existing.AssemblyName,
                    definition.AssemblyName,
                    StringComparison.Ordinal)
                    ? $"more than once in assembly '{definition.AssemblyName}' by " +
                      $"'{existing.TypeName}' and '{definition.TypeName}'"
                    : $"by '{existing.TypeName}' in assembly '{existing.AssemblyName}' and " +
                      $"'{definition.TypeName}' in assembly '{definition.AssemblyName}'";
                diagnostics.Add(new MarkupSchemaDiagnostic(
                    MarkupDiagnosticCodes.InvalidControlSchema,
                    $"Markup element '{definition.QualifiedName}' is declared {declaration}."));
                continue;
            }

            controls.Add(key, definition);
            if (!controlsByName.TryGetValue(definition.Name, out var definitions)) {
                definitions = [];
                controlsByName.Add(definition.Name, definitions);
            }

            definitions.Add(definition);
        }

        return new ControlCatalog(
            compilation.AssemblyName ?? string.Empty,
            controls,
            controlsByName,
            diagnostics);
    }

    public ControlDefinition? Find(string namespaceName, string name) {
        return _controls.TryGetValue(Key(namespaceName, name), out var definition)
            ? definition
            : null;
    }

    public ControlDefinition? Find(
        string namespaceName,
        string? assemblyName,
        string name) {
        var definition = Find(namespaceName, name);
        return definition is not null &&
               (string.IsNullOrEmpty(assemblyName) ||
                string.Equals(
                    definition.AssemblyName,
                    assemblyName,
                    StringComparison.Ordinal))
            ? definition
            : null;
    }

    public IReadOnlyList<ControlDefinition> FindAll(string name) {
        return _controlsByName.TryGetValue(name, out var definitions)
            ? definitions
            : Array.Empty<ControlDefinition>();
    }

    private static string Key(string namespaceName, string name) {
        return namespaceName + "\0" + name;
    }
}
