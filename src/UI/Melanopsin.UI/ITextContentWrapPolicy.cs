// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

/// <summary>
/// Overrides the default wrapping behavior for text content controls.
/// </summary>
public interface ITextContentWrapPolicy {
    bool WrapText { get; }
}
