// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

/// <summary>
/// Canonical names for states shared by the UI runtime and common controls.
/// Applications may still invent additional state names for their own controls.
/// </summary>
public static class UiStateNames {
    public const string Hover = "hover";
    public const string Focused = "focused";
    public const string Disabled = "disabled";
    public const string Pressed = "pressed";
    public const string Checked = "checked";
    public const string Selected = "selected";
    public const string Expanded = "expanded";
    public const string ReadOnly = "read-only";
    public const string Invalid = "invalid";
    public const string Busy = "busy";
    public const string Entering = "entering";
    public const string Exiting = "exiting";
    public const string Moving = "moving";
}
