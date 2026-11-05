// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

internal sealed record RichLayout(
    IReadOnlyList<RichLine> Lines,
    float Width,
    float Height,
    float Ascent,
    float Descent);
