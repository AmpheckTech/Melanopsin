// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.Graphics.Rendering;

namespace Melanopsin.UI.Rendering;

internal sealed class BoundaryLayerCache {
    public BoundaryLayerCache(
        long paintRevision,
        Rect bounds,
        Rect? clipBounds,
        bool drawDebugBounds,
        RenderLayer layer) {
        PaintRevision = paintRevision;
        Bounds = bounds;
        ClipBounds = clipBounds;
        DrawDebugBounds = drawDebugBounds;
        Layer = layer;
    }

    private long PaintRevision { get; }

    private Rect Bounds { get; }

    private Rect? ClipBounds { get; }

    private bool DrawDebugBounds { get; }

    public RenderLayer Layer { get; }

    public bool IsValid(LayoutNode layoutNode, RenderOptions options) {
        return PaintRevision == layoutNode.Node.SubtreePaintRevision &&
               Bounds == layoutNode.Bounds &&
               ClipBounds == layoutNode.ClipBounds &&
               DrawDebugBounds == options.DrawDebugBounds;
    }
}
