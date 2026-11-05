// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

public readonly record struct TextCaretPosition(
    int TextOffset,
    float X,
    int LineIndex,
    float LineTop,
    float LineHeight);
