// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup;

/// <summary>
/// Describes the compile-time validation applied to a static markup attribute value.
/// </summary>
public enum MarkupValueKind {
    Any,
    Number,
    Length,
    Color,
    Integer,
    Boolean,
    Enum,
    Input,
}
