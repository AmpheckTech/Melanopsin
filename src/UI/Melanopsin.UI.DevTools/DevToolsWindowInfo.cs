// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed record DevToolsWindowInfo(
    WindowId Id,
    string Title,
    string CssClass);
