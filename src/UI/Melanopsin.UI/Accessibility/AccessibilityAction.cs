// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Accessibility;

/// <summary>
/// Standard actions that a platform accessibility bridge can request.
/// A provider may expose several flags, while one invocation targets one action.
/// </summary>
[Flags]
public enum AccessibilityAction {
    None = 0,
    Default = 1 << 0,
    Toggle = 1 << 1,
    Select = 1 << 2,
    Expand = 1 << 3,
    Collapse = 1 << 4,
    Increment = 1 << 5,
    Decrement = 1 << 6,
}
