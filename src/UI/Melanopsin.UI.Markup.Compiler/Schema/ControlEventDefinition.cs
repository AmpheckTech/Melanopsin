// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed record ControlEventDefinition(
    string Name,
    string DelegateTypeName,
    int ParameterCount,
    string? ArgumentTypeName);
