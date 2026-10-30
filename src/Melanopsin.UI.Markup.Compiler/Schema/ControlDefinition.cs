using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed record ControlDefinition(
    string Name,
    string TypeName,
    IReadOnlyDictionary<string, MarkupAttributeDefinition> Attributes);
