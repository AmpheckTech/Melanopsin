using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed record ControlDefinition(
    string Name,
    string Namespace,
    string AssemblyName,
    string TypeName,
    IReadOnlyDictionary<string, MarkupAttributeDefinition> Attributes,
    IReadOnlyDictionary<string, ControlEventDefinition> Events,
    bool IsComponent = false) {
    public string QualifiedName => Namespace.Length == 0
        ? Name
        : Namespace + "." + Name;

    public string ElementTypeName => IsComponent
        ? "global::Melanopsin.UI.ComponentHost"
        : TypeName;
}
