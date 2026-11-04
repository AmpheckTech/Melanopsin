using System.Diagnostics;
using Melanopsin.UI.Layout;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class Window {
    internal bool TryRender(
        RenderOptions options,
        out RenderFrameLease frame) {
        ThrowIfDisposed();
        DrainRetiredTargets();
        if (!_target.IsFrameAvailable) {
            frame = default;
            return false;
        }

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
        if (!_target.TryRender(scene, out frame)) {
            return false;
        }

        var renderMilliseconds =
            Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;

        if (frame.Surface.Size != Size) {
            frame.Release();
            throw new InvalidOperationException(
                "The rendering backend returned a frame for the wrong size.");
        }

        _stagedRenderOptions = options;
        _stagedStatistics = frame.Statistics;
        _stagedDamage = damage;
        _stagedLayoutMilliseconds = layoutMilliseconds;
        _stagedComposeMilliseconds = composeMilliseconds;
        _stagedRenderMilliseconds = renderMilliseconds;
        _hasStagedFrame = true;
        return true;
    }

    internal void CommitStagedFrame() {
        if (!_hasStagedFrame || _stagedDamage is not { } damage) {
            throw new InvalidOperationException(
                "There is no rendered frame to commit.");
        }

        _hasStagedFrame = false;
        _pendingDamage.Clear();
        _invalidatePaintCache = false;
        _forceFullLayoutDamage = false;
        _lastRenderOptions = _stagedRenderOptions;
        _hasRendered = true;
        LastRenderStatistics = _stagedStatistics;
        _frameNumber++;
        var layout = _lastLayout ?? throw new InvalidOperationException(
            "The layout pass completed without producing a layout snapshot.");
        var damageArea = damage.Rects.Sum(static rect => rect.Area);
        _devToolsSnapshot = new DevToolsSnapshot(
            _frameNumber,
            LayoutVersion,
            _stagedStatistics.CommandCount,
            damage.Rects.Count,
            damageArea,
            _stagedStatistics.ReusedPaintBoundaries,
            _stagedStatistics.RepaintedPaintBoundaries,
            _stagedStatistics.RasterCacheHits,
            _stagedStatistics.RasterCacheMisses,
            _stagedStatistics.RasterCacheBytes,
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
            _stagedLayoutMilliseconds,
            _stagedComposeMilliseconds,
            _stagedRenderMilliseconds,
            _stagedRenderOptions.VisualAccessibility);
        _stagedDamage = null;
        DiagnosticsChanged?.Invoke(this, _devToolsSnapshot.Value);
    }

    internal void DiscardStagedFrame() {
        _hasStagedFrame = false;
        _stagedDamage = null;
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
