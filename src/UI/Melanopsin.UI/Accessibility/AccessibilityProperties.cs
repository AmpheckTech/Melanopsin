// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Accessibility;

public sealed class AccessibilityProperties {
    public AccessibilityProperties(AccessibilityRole role) {
        Role = role;
    }

    public AccessibilityRole Role { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Value { get; set; }

    public bool IsHidden { get; set; }
}
