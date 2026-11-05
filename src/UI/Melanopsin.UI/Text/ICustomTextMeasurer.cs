// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

public interface ICustomTextMeasurer {
    TextMetrics MeasureTextLayout(
        ITextLayout textLayout,
        float maxWidth,
        float defaultFontSize,
        FontFace? defaultFont = null);
}
