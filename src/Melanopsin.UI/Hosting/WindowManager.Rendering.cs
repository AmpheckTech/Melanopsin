using System.Diagnostics;
using Melanopsin.UI.App;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.Hosting;

public sealed partial class WindowManager {
    public IReadOnlyList<RenderFrame> RenderAll(RenderOptions? options = null) {
        ThrowIfDisposed();
        return RenderWindows(options ?? RenderOptions.Default, dirtyOnly: false);
    }

    public IReadOnlyList<RenderFrame> RenderDirty(RenderOptions? options = null) {
        ThrowIfDisposed();
        return RenderWindows(options ?? RenderOptions.Default, dirtyOnly: true);
    }

    private IReadOnlyList<RenderFrame> RenderWindows(
        RenderOptions options,
        bool dirtyOnly) {
        var frames = new List<RenderFrame>(_windows.Count);
        var layoutChanged = false;

        foreach (var window in _windows) {
            if (dirtyOnly && !window.IsDirty) {
                continue;
            }

            // Do not draw into an offscreen target while the platform is
            // waiting for the compositor's frame callback. This mirrors
            // Avalonia's RenderTargetNotReady/State.IsReady behavior and keeps
            // interactive resize frames coalesced instead of rendering every
            // configure event.
            if (_platformHost is not null &&
                !_platformHost.IsPresentationReady(window.Descriptor)) {
                continue;
            }

            var layoutRevision = window.LayoutVersion;
            var renderStarted = Stopwatch.GetTimestamp();
            var windowOptions = window.ShowDebugBounds
                ? options with { DrawDebugBounds = true }
                : options;
            var frame = window.Render(windowOptions);

            var presentStarted = Stopwatch.GetTimestamp();
            _presenter?.Present(window.Descriptor, frame);
            if (TraceFrameTiming) {
                Console.Error.WriteLine(
                    $"[ui] frame {window.Id} {frame.Descriptor.Size.Width}x{frame.Descriptor.Size.Height} " +
                    $"layout={window.LastDiagnostics?.LayoutMilliseconds ?? 0:0.00}ms " +
                    $"compose={window.LastDiagnostics?.ComposeMilliseconds ?? 0:0.00}ms " +
                    $"raster={window.LastDiagnostics?.RenderMilliseconds ?? 0:0.00}ms " +
                    $"render-total={Stopwatch.GetElapsedTime(renderStarted, presentStarted).TotalMilliseconds:0.00}ms " +
                    $"present-call={Stopwatch.GetElapsedTime(presentStarted).TotalMilliseconds:0.00}ms");
            }

            TotalFramesPresented++;
            window.MarkClean();
            layoutChanged |= window.LayoutVersion != layoutRevision;
            frames.Add(frame);
        }

        if (layoutChanged) {
            PruneStyleState();
        }

        return frames;
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
