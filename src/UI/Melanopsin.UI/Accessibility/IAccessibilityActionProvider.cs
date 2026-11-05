// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Accessibility;

/// <summary>
/// Implemented by controls that expose platform-independent accessibility actions.
/// </summary>
public interface IAccessibilityActionProvider {
    AccessibilityAction SupportedActions { get; }

    bool PerformAccessibilityAction(AccessibilityAction action);
}
