using System;
using System.Collections.Generic;
using System.Threading;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Schema.Discovery;
using Microsoft.CodeAnalysis;

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed class ControlCatalog {
    private readonly Dictionary<string, ControlDefinition> _controls;

    private ControlCatalog(
        Dictionary<string, ControlDefinition> controls,
        IReadOnlyList<MarkupSchemaDiagnostic> diagnostics) {
        _controls = controls;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<MarkupSchemaDiagnostic> Diagnostics { get; }

    public static ControlCatalog Create(
        Compilation compilation,
        CancellationToken cancellationToken) {
        var controls = new Dictionary<string, ControlDefinition>(StringComparer.Ordinal);
        var diagnostics = new List<MarkupSchemaDiagnostic>();

        foreach (var type in ControlTypeDiscovery.Enumerate(compilation, cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = ControlDefinitionFactory.Create(compilation, type, diagnostics);
            if (definition is null) {
                continue;
            }

            if (controls.TryGetValue(definition.Name, out var existing)) {
                if (!string.Equals(existing.TypeName, definition.TypeName, StringComparison.Ordinal)) {
                    diagnostics.Add(new MarkupSchemaDiagnostic(
                        MarkupDiagnosticCodes.InvalidControlSchema,
                        $"Markup element '{definition.Name}' is declared by both " +
                        $"'{existing.TypeName}' and '{definition.TypeName}'."));
                }

                continue;
            }

            controls.Add(definition.Name, definition);
        }

        return new ControlCatalog(controls, diagnostics);
    }

    public ControlDefinition? Find(string name) {
        return _controls.TryGetValue(name, out var definition)
            ? definition
            : null;
    }
}
