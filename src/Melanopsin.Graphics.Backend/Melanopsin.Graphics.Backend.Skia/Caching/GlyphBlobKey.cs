// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Text;

namespace Melanopsin.Graphics.Backend.Skia;

internal readonly record struct GlyphBlobKey(
    TextRun Run,
    float Baseline,
    float OriginX);
