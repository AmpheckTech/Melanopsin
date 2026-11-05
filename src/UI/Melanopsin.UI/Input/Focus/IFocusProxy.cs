// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;

namespace Melanopsin.UI.Input;

/// <summary>
/// Redirects focus for transient controls such as popups without moving focus
/// away from the control that owns them.
/// </summary>
public interface IFocusProxy {
    Node FocusTarget { get; }
}
