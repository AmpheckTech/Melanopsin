// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

public interface ILayoutEngine {
    LayoutSnapshot Layout(
        Node root,
        PixelSize size,
        LayoutPassContext context = default);
}
