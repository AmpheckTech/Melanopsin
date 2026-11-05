// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.App;

/// <summary>
/// Read-only access to live window trees for input, focus and accessibility.
/// Implementations own rendering and layout orchestration; consumers only
/// observe stable tree data.
/// </summary>
public interface IWindowTreeQuery {
    IEnumerable<WindowId> WindowIds { get; }

    bool ContainsWindow(WindowId windowId);

    Node? GetRoot(WindowId windowId);

    bool TryGetBounds(WindowId windowId, Node node, out Rect bounds);
}
