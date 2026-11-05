// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

/// <summary>
/// Describes the earliest pipeline stage affected by a node change.
/// </summary>
[Flags]
public enum InvalidationKind {
    None = 0,
    Paint = 1,
    Layout = 2,
    LayoutAndPaint = Paint | Layout,
    Build = 4,
    Style = 8,
}
