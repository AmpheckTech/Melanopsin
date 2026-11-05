using Melanopsin.UI;
using Melanopsin.UI.Styling;

namespace Melanopsin.UI.Layout;

/// <summary>
/// Engine services available while a layout participant arranges its children.
/// </summary>
public sealed class LayoutArrangeContext {
    private readonly FlowLayoutEngine _engine;
    private readonly LayoutNode _layoutNode;

    internal LayoutArrangeContext(
        FlowLayoutEngine engine,
        LayoutNode layoutNode,
        Rect bounds,
        Rect? childClipBounds,
        ResolvedStyle style,
        UiMetrics metrics) {
        _engine = engine;
        _layoutNode = layoutNode;
        Bounds = bounds;
        ChildClipBounds = childClipBounds;
        Style = style;
        Metrics = metrics;
    }

    public Rect Bounds { get; }

    /// <summary>
    /// Clip inherited by children after applying this node's own clip.
    /// </summary>
    public Rect? ChildClipBounds { get; }

    public ResolvedStyle Style { get; }

    public UiMetrics Metrics { get; }

    public Rect ContentBounds => new(
        Bounds.MinX + Style.Padding,
        Bounds.MinY + Style.Padding,
        Math.Max(0, Bounds.Width - (Style.Padding * 2)),
        Math.Max(0, Bounds.Height - (Style.Padding * 2)));

    public LayoutSize Measure(Node node, LayoutConstraints constraints) {
        return _engine.MeasureNode(node, constraints);
    }

    public float MeasureIntrinsic(
        Node node,
        LayoutAxis axis,
        LayoutIntrinsicMode mode,
        float crossAxisExtent = float.PositiveInfinity) {
        return _engine.MeasureIntrinsic(
            node,
            axis,
            mode,
            crossAxisExtent);
    }

    public ResolvedStyle GetStyle(Node node) {
        return _engine.ResolveStyle(node);
    }

    public IReadOnlyList<Node> GetVisibleChildren(Element element) {
        return _engine.GetVisibleChildren(element);
    }

    public void ArrangeChild(Node child, Rect bounds) {
        ArgumentNullException.ThrowIfNull(child);
        _layoutNode.Children.Add(_engine.ArrangeNode(
            child,
            bounds,
            _layoutNode,
            ChildClipBounds));
    }
}
