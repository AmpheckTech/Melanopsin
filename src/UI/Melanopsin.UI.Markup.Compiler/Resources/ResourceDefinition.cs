// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed record ResourceDefinition(
    string Category,
    string Name,
    string Id,
    string Kind,
    string ValueTypeName,
    IReadOnlyList<string> Variants);
