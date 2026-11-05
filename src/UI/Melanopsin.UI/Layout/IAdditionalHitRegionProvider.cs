// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Layout;

/// <summary>
/// Supplies hit regions for interactive decorations that are painted outside
/// the normal child layout tree, such as scrollbars.
/// </summary>
public interface IAdditionalHitRegionProvider {
    void AppendHitRegionBounds(List<Rect> bounds);
}
