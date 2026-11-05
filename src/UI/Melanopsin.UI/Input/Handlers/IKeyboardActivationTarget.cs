// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Input;

/// <summary>
/// A focusable element that can be invoked with Enter or Space.
/// </summary>
public interface IKeyboardActivationTarget {
    void Activate();
}
