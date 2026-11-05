// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text;

public readonly record struct TextGlyph(
    uint Id,
    int Cluster,
    float XAdvance,
    float YAdvance,
    float XOffset,
    float YOffset);
