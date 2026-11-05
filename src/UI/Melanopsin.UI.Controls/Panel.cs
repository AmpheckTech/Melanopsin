// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Markup;

namespace Melanopsin.UI.Controls;

[MarkupElement]
public class Panel : LinearLayout {
    public Panel()
        : base("Panel") {
    }
}
