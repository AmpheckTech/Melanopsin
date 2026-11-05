// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Diagnostics;

internal sealed record MarkupDiagnostic(
    string Code,
    string Message,
    int Offset,
    int Length);
