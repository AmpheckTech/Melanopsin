// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

internal readonly record struct TextLayoutKey(
    string Text,
    float FontSize,
    float MaxWidth,
    TextDirection Direction,
    FontFace? Font,
    long FontVersion);
