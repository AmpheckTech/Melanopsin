using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

/// <summary>
/// Supplies content-based intrinsic sizes to parent layouts.
/// </summary>
public interface ILayoutIntrinsicProvider {
    /// <summary>
    /// Measures an intrinsic axis. For a horizontal axis,
    /// <paramref name="crossAxisExtent"/> is the available height; for a
    /// vertical axis it is the available width. Infinity means unconstrained.
    /// </summary>
    float MeasureIntrinsic(
        LayoutMeasureContext context,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent);
}
