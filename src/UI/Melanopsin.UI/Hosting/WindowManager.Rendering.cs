// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Melanopsin.UI.App;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class WindowManager {
    public int RenderAll(RenderOptions? options = null) {
        ThrowIfDisposed();
        return RenderWindows(options ?? RenderOptions.Default, dirtyOnly: false);
    }

    public int RenderDirty(RenderOptions? options = null) {
        ThrowIfDisposed();
        return RenderWindows(options ?? RenderOptions.Default, dirtyOnly: true);
    }

    private int RenderWindows(
        RenderOptions options,
        bool dirtyOnly) {
        if (_presenter is null) {
            throw new InvalidOperationException(
                "Rendering requires a frame presenter.");
        }

        var presentedFrames = 0;
        var layoutChanged = false;

        foreach (var window in _windows) {
            window.DrainRetiredTargets();
            if (dirtyOnly && !window.IsDirty) {
                continue;
            }

            // Do not draw into an offscreen target while the platform is
            // waiting for the compositor's frame callback. This coalesces
            // interactive resize frames instead of rendering every configure
            // event for a frame the platform cannot accept.
            if (_platformHost is not null &&
                !_platformHost.IsPresentationReady(window.Descriptor)) {
                continue;
            }

            var layoutRevision = window.LayoutVersion;
            var renderStarted = Stopwatch.GetTimestamp();
            var windowOptions = window.ShowDebugBounds
                ? options with { DrawDebugBounds = true }
                : options;
            if (!window.TryRender(windowOptions, out var frame)) {
                continue;
            }

            var presentStarted = Stopwatch.GetTimestamp();
            PresentResult result;
            try {
                result = _presenter.Present(window.Descriptor, frame);
            } catch {
                frame.Release();
                window.DiscardStagedFrame();
                throw;
            }

            if (TraceFrameTiming) {
                Console.Error.WriteLine(
                    $"[ui] frame {window.Id} {window.Size.Width}x{window.Size.Height} " +
                    $"layout={window.LastDiagnostics?.LayoutMilliseconds ?? 0:0.00}ms " +
                    $"compose={window.LastDiagnostics?.ComposeMilliseconds ?? 0:0.00}ms " +
                    $"raster={window.LastDiagnostics?.RenderMilliseconds ?? 0:0.00}ms " +
                    $"render-total={Stopwatch.GetElapsedTime(renderStarted, presentStarted).TotalMilliseconds:0.00}ms " +
                    $"present-call={Stopwatch.GetElapsedTime(presentStarted).TotalMilliseconds:0.00}ms");
            }

            if (result != PresentResult.Accepted) {
                frame.Release();
                window.DiscardStagedFrame();
                continue;
            }

            window.CommitStagedFrame();
            window.MarkClean();
            TotalFramesPresented++;
            presentedFrames++;
            layoutChanged |= window.LayoutVersion != layoutRevision;
        }

        if (layoutChanged) {
            PruneStyleState();
        }

        return presentedFrames;
    }

    private IRenderTarget ReplaceTarget(RenderTargetDescriptor descriptor) {
        ThrowIfDisposed();
        return _backend.CreateTarget(descriptor);
    }

    private void PruneStyleState() {
        var liveNodes = new HashSet<Node>(
            ReferenceEqualityComparer.Instance);
        foreach (var window in _windows) {
            if (window.Layout is not { } layout) {
                continue;
            }

            foreach (var node in layout.Nodes) {
                liveNodes.Add(node);
            }
        }

        foreach (var window in _windows) {
            window.RenderContext.Prune(liveNodes);
        }
    }
}
