// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Text.Fonts;

namespace Melanopsin.Graphics.Text;

public interface ITextLayout : ITextMeasurer {
    ShapedText Layout(string text, in TextLayoutOptions options);

    TextCaretPosition HitTestCaret(
        string text,
        float x,
        in TextLayoutOptions options);
}
