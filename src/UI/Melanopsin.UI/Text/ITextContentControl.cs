// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI;

public interface ITextContentControl {
    string Text { get; set; }

    void AppendText(string value);
}
