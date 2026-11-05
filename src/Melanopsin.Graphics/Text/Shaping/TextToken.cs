// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

internal readonly record struct TextToken(
    int Start,
    int Length,
    bool IsWhitespace,
    bool IsHardBreak);
