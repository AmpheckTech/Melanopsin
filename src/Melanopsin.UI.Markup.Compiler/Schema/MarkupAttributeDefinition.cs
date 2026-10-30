using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed record MarkupAttributeDefinition(
    string Name,
    SchemaValueKind ValueKind,
    IReadOnlyCollection<string> AllowedValues,
    string? ValueTypeName,
    string? PropertyName,
    string? PropertyTypeName,
    IReadOnlyDictionary<string, string> EnumMembers);
