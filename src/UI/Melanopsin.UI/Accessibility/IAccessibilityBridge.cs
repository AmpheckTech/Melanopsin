// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Accessibility;

public interface IAccessibilityBridge {
    void Publish(AccessibilityTree tree);
}
