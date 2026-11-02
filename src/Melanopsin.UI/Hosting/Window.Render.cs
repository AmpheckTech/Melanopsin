using System.Diagnostics;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    internal RenderFrame Render(RenderOptions options) {
        ThrowIfDisposed();
        options = options with {
            ScaleFactor = DisplayScale,
        };
        _renderContext.UpdateMetrics(Size, DisplayScale);
        if (_hasRendered && options != _lastRenderOptions) {
            _invalidatePaintCache = true;
            _forceFullLayoutDamage = true;
            _pendingDamage.MarkFull();
        }

        var layoutRan = false;
        var layoutMilliseconds = 0d;
        FlushPendingBuilds(Root);
        var previousLayout = _lastLayout;
        var canDiffLayout = _isLayoutDirty &&
            previousLayout is not null &&
            !_forceFullLayoutDamage;
        if (canDiffLayout) {
            CaptureLayoutGeometry(previousLayout!);
        }

        if (_isLayoutDirty || _lastLayout is null) {
            var layoutStart = Stopwatch.GetTimestamp();
            _lastLayout = _renderContext.Layout.Layout(
                Root,
                Size,
                new LayoutPassContext(
                    invalidateCache: _invalidateLayoutCache,
                    viewportChanged: _viewportChanged,
                    invalidatedLayoutNodes: _invalidatedLayoutNodes,
                    forceFullInvalidation: _forceFullLayoutInvalidation));
            _isLayoutDirty = false;
            _invalidateLayoutCache = false;
            _viewportChanged = false;
            _forceFullLayoutInvalidation = false;
            _invalidatedLayoutNodes.Clear();
            LayoutVersion++;
            layoutRan = true;
            layoutMilliseconds = Stopwatch.GetElapsedTime(layoutStart).TotalMilliseconds;
        }

        if (layoutRan) {
            if (_forceFullLayoutDamage || !canDiffLayout) {
                _previousLayoutGeometry.Clear();
                _pendingDamage.MarkFull();
            } else {
                AddLayoutDamage(_lastLayout!);
            }
        }

        var layout = _lastLayout ?? throw new InvalidOperationException(
            "The layout pass completed without producing a layout snapshot.");
        var damage = _pendingDamage.Build();
        if (damage.IsEmpty && !_hasRendered) {
            damage = DamageRegion.Full(Size);
        }

        var damageArea = damage.Rects.Sum(static rect => rect.Area);
        var composeStart = Stopwatch.GetTimestamp();
        var scene = _renderContext.Composer.Compose(
            layout,
            new RenderCompositionContext(
                options,
                damage,
                _invalidatePaintCache));
        var composeMilliseconds =
            Stopwatch.GetElapsedTime(composeStart).TotalMilliseconds;
        var renderStart = Stopwatch.GetTimestamp();
        var frame = _target.Render(scene);
        var renderMilliseconds =
            Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;

        if (frame.Descriptor.Size != Size) {
            throw new InvalidOperationException(
                "The rendering backend returned a frame for the wrong size.");
        }

        _pendingDamage = new DamageRegionBuilder(Size);
        _invalidatePaintCache = false;
        _forceFullLayoutDamage = false;
        _lastRenderOptions = options;
        _hasRendered = true;
        LastRenderStatistics = frame.Statistics;
        _frameNumber++;
        _devToolsSnapshot = new DevToolsSnapshot(
            _frameNumber,
            LayoutVersion,
            frame.Statistics.CommandCount,
            damage.Rects.Count,
            damageArea,
            frame.Statistics.ReusedPaintBoundaries,
            frame.Statistics.RepaintedPaintBoundaries,
            frame.Statistics.RasterCacheHits,
            frame.Statistics.RasterCacheMisses,
            frame.Statistics.RasterCacheBytes,
            layout.Statistics.InvalidationScope,
            layout.Statistics.InvalidationPathNodeCount,
            layout.Statistics.RelayoutBoundaryHits,
            layout.Statistics.RelayoutBoundaryMisses,
            layout.Statistics.ArrangeReuseHits,
            layout.Statistics.ArrangeReuseMisses,
            layout.Statistics.HitRegionCacheHits,
            layout.Statistics.HitRegionCacheMisses,
            layout.Statistics.MeasureExecutions,
            layout.Statistics.PersistentMeasureHits,
            layout.Nodes.Count,
            layout.HitRegions.Count,
            layoutMilliseconds,
            composeMilliseconds,
            renderMilliseconds,
            options.VisualAccessibility);
        DiagnosticsChanged?.Invoke(this, _devToolsSnapshot.Value);
        return frame;
    }

    internal bool AdvanceAnimations(float elapsedSeconds) {
        ThrowIfDisposed();
        var changed = _renderContext.AdvanceAnimations(Root, elapsedSeconds);
        changed |= AdvanceNodeAnimations(Root, elapsedSeconds);
        return changed;
    }

    private static bool AdvanceNodeAnimations(Node node, float elapsedSeconds) {
        var changed = node is IAnimationParticipant animationParticipant &&
            animationParticipant.AdvanceAnimation(elapsedSeconds);
        changed |= node is ScrollViewport viewport &&
            viewport.AdvanceAnimation(elapsedSeconds);
        changed |= node is PresenceScope presence &&
            presence.AdvanceAnimation(elapsedSeconds);
        if (node is Element element) {
            foreach (var child in element.Children) {
                changed |= AdvanceNodeAnimations(child, elapsedSeconds);
            }
        }

        return changed;
    }

    private static void FlushPendingBuilds(Node node) {
        if (node is ComponentHost host) {
            host.FlushPendingBuild();
        }

        if (node is Element element) {
            foreach (var child in element.Children) {
                FlushPendingBuilds(child);
            }
        }
    }
}
