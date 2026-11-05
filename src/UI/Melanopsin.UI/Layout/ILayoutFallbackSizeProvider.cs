// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Layout;

/// <summary>
/// Supplies a control-specific fallback height when no explicit or intrinsic
/// height is available.
/// </summary>
public interface ILayoutFallbackSizeProvider {
    float MeasureFallbackHeight(
        LayoutMeasureContext context,
        LayoutConstraints constraints);
}
