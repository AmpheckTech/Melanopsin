// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Accessibility;

/// <summary>
/// Standard state flags exposed to platform accessibility bridges.
/// </summary>
[Flags]
public enum AccessibilityState {
    None = 0,
    Disabled = 1 << 0,
    Pressed = 1 << 1,
    Checked = 1 << 2,
    Selected = 1 << 3,
    Expanded = 1 << 4,
    ReadOnly = 1 << 5,
    Invalid = 1 << 6,
    Busy = 1 << 7,
}
