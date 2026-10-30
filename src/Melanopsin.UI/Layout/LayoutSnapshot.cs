namespace Melanopsin.UI.Layout;

public sealed class LayoutSnapshot {
    private readonly Dictionary<Node, LayoutNode> _nodes;

    public LayoutSnapshot(
        PixelSize size,
        LayoutNode root,
        IReadOnlyList<HitRegion> hitRegions,
        LayoutStatistics? statistics = null) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(hitRegions);
        Size = size;
        Root = root;
        HitRegions = [.. hitRegions];
        Statistics = statistics ??
            new LayoutStatistics(0, 0, 0, 0);
        _nodes = [];
        CollectNodes(root, _nodes);
    }

    public PixelSize Size { get; }

    public LayoutNode Root { get; }

    public IReadOnlyList<HitRegion> HitRegions { get; }

    public LayoutStatistics Statistics { get; }

    public IReadOnlyCollection<Node> Nodes => _nodes.Keys;

    public bool TryGetNode(Node node, out LayoutNode layoutNode) {
        ArgumentNullException.ThrowIfNull(node);
        return _nodes.TryGetValue(node, out layoutNode!);
    }

    public bool Contains(Node node) {
        ArgumentNullException.ThrowIfNull(node);
        return _nodes.ContainsKey(node);
    }

    public HitRegion? HitTest(float x, float y) {
        for (var index = HitRegions.Count - 1; index >= 0; index--) {
            if (HitRegions[index].Contains(x, y, this)) {
                return HitRegions[index];
            }
        }

        return null;
    }

    private static void CollectNodes(
        LayoutNode layoutNode,
        Dictionary<Node, LayoutNode> nodes) {
        nodes[layoutNode.Node] = layoutNode;
        foreach (var child in layoutNode.Children) {
            CollectNodes(child, nodes);
        }
    }
}
