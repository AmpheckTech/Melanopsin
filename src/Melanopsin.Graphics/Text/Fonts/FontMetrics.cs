// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.Graphics.Text.Fonts;

public readonly record struct FontMetrics(
    int UnitsPerEm,
    int Ascender,
    int Descender,
    int LineGap) {
    public float Scale(float fontSize) {
        return fontSize / UnitsPerEm;
    }

    public float LineHeight(float fontSize) {
        return (Ascender - Descender + LineGap) * Scale(fontSize);
    }
}
