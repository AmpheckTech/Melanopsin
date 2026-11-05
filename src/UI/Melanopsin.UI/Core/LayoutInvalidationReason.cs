// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

/// <summary>
/// Explains why a layout invalidation can be narrowed to a source path or
/// must conservatively traverse the window tree.
/// </summary>
public enum LayoutInvalidationReason {
    None,
    LayoutProperty,
    Structure,
    Style,
    Metrics,
    Unknown,
}
