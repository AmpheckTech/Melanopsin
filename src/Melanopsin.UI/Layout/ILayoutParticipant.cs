using Melanopsin.UI;

namespace Melanopsin.UI.Layout;

/// <summary>
/// Allows a control to own its measure and arrange behavior without adding a
/// control-specific branch to the layout engine.
/// </summary>
public interface ILayoutParticipant {
    LayoutSize Measure(
        LayoutMeasureContext context,
        LayoutConstraints constraints);

    void Arrange(LayoutArrangeContext context, Rect bounds);
}
