using System.Diagnostics;
using Melanopsin.UI;

namespace Melanopsin.UI.Platform.Wayland;

public sealed partial class WaylandPlatformHost {
    private void HandleFrameCallbackDone(WindowId windowId) {
        if (_frameCallbackTimestamps.Remove(
                windowId,
                out var callbackTimestamp) &&
            TracePresentation) {
            Console.Error.WriteLine(
                $"[wayland] frame callback {windowId}: " +
                $"{Stopwatch.GetElapsedTime(callbackTimestamp).TotalMilliseconds:0.00}ms");
        }

        RetryPendingFrames();
    }

    private void RetryPendingFrames() {
        if (_pendingFrames.Count == 0) {
            return;
        }

        foreach (var windowId in _pendingFrames.Keys.ToArray()) {
            _ = TryPresent(windowId);
        }
    }

    private bool TryPresent(WindowId windowId) {
        if (!_pendingFrames.TryGetValue(windowId, out var pending) ||
            !_windows.TryGetValue(windowId, out var window)) {
            return false;
        }

        var size = new PixelSize(pending.Width, pending.Height);
        if (!IsPendingFrameCurrent(pending, window)) {
            DropStalePendingFrame(windowId, window);
            return false;
        }

        if (window.IsFrameCallbackPending) {
            if (TracePresentation &&
                _pendingFrameTimestamps.TryGetValue(windowId, out var queuedAt)) {
                Trace(
                    $"gpu frame queued behind callback {windowId}: " +
                    $"{Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:0.00}ms");
            }

            return false;
        }

        if (!window.CanPresent(size)) {
            return false;
        }

        if (pending.Frame.GpuSurface is { } gpuSurface) {
            window.ConfigureBuffer(pending.Width, pending.Height);
            if (!window.ArmFrameCallback(
                    () => HandleFrameCallbackDone(windowId))) {
                Trace("gpu frame callback already pending");
                return false;
            }

            if (TracePresentation) {
                _frameCallbackTimestamps[windowId] = Stopwatch.GetTimestamp();
            }

            bool presented;
            var presentStart = Stopwatch.GetTimestamp();
            try {
                presented = gpuSurface.TryPresent(window.Handle);
            } catch {
                window.CancelFrameCallback();
                _frameCallbackTimestamps.Remove(windowId);
                throw;
            }

            if (TracePresentation) {
                Console.Error.WriteLine(
                    $"[wayland] gpu present {pending.Width}x{pending.Height} " +
                    $"window={window.Size.Width}x{window.Size.Height} " +
                    $"result={(presented ? "ok" : "fallback")} " +
                    $"{Stopwatch.GetElapsedTime(presentStart).TotalMilliseconds:0.00}ms " +
                    $"queued={Stopwatch.GetElapsedTime(
                        _pendingFrameTimestamps.GetValueOrDefault(windowId, presentStart)).TotalMilliseconds:0.00}ms");
            }

            if (presented) {
                // EGL owns the commit, but do not assume it flushed the
                // connection; a missing flush turns the next wait into a
                // timeout-paced resize instead of a compositor-paced one.
                window.NotifySurfaceCommitted();
                _connection.FlushChecked();
                window.GpuSurface = gpuSurface;
                _pendingFrames.Remove(windowId);
                _pendingFrameTimestamps.Remove(windowId);
                return true;
            }

            // EGL is unavailable for this backend/window. Drop the callback
            // before falling back to wl_shm so the next attempt can proceed.
            window.CancelFrameCallback();
            _frameCallbackTimestamps.Remove(windowId);
            ReleaseWindowGpuSurface(window);
        }

        window.ConfigureBuffer(pending.Width, pending.Height);
        if (!window.TryAcquireBuffer(
                pending.Width,
                pending.Height,
                pending.Stride,
                out var buffer) ||
            buffer is null) {
            Trace("shm buffer unavailable");
            return false;
        }

        if (!window.ArmFrameCallback(RetryPendingFrames)) {
            buffer.ReleaseReservation();
            Trace("shm frame callback already pending");
            return false;
        }

        var cpuPresentStart = Stopwatch.GetTimestamp();
        try {
            buffer.Commit(pending.Pixels.Span);
            window.Surface.Attach(buffer.Buffer, 0, 0);
            window.Surface.DamageBuffer(
                0,
                0,
                pending.Width,
                pending.Height);
            window.Surface.Commit();
        } catch {
            buffer.AbortCommit();
            window.CancelFrameCallback();
            throw;
        }

        window.NotifySurfaceCommitted();
        _connection.FlushChecked();
        if (TracePresentation) {
            Console.Error.WriteLine(
                $"[wayland] shm present {pending.Width}x{pending.Height} " +
                $"window={window.Size.Width}x{window.Size.Height} " +
                $"{Stopwatch.GetElapsedTime(cpuPresentStart).TotalMilliseconds:0.00}ms");
        }

        ReleaseWindowGpuSurface(window);
        _pendingFrames.Remove(windowId);
        _pendingFrameTimestamps.Remove(windowId);
        return true;
    }

    private static WaylandPendingFrame CreatePendingFrame(
        WaylandWindow window,
        WindowDescriptor descriptor,
        RenderFrame frame) {
        return new WaylandPendingFrame(
            descriptor,
            frame,
            window.LogicalSize.Width,
            window.LogicalSize.Height,
            window.DisplayScale);
    }

    private static void ReleaseWindowGpuSurface(WaylandWindow window) {
        if (window.GpuSurface is { } gpuSurface) {
            gpuSurface.ReleasePresentSurface(window.Handle);
        }

        window.GpuSurface = null;
    }

    private static bool IsPendingFrameCurrent(
        WaylandPendingFrame pending,
        WaylandWindow window) {
        return pending.Width == window.Size.Width &&
            pending.Height == window.Size.Height &&
            pending.LogicalWidth == window.LogicalSize.Width &&
            pending.LogicalHeight == window.LogicalSize.Height &&
            Math.Abs(pending.DisplayScale - window.DisplayScale) < 0.0001f;
    }

    private void DropStalePendingFrame(
        WindowId windowId,
        WaylandWindow window) {
        if (!_pendingFrames.TryGetValue(windowId, out var pending) ||
            IsPendingFrameCurrent(pending, window)) {
            return;
        }

        _pendingFrames.Remove(windowId);
        _pendingFrameTimestamps.Remove(windowId);
        if (TracePresentation) {
            Console.Error.WriteLine(
                $"[wayland] dropped stale pending frame {windowId} " +
                $"{pending.Width}x{pending.Height} " +
                $"logical={pending.LogicalWidth}x{pending.LogicalHeight} " +
                $"scale={pending.DisplayScale:0.###} for " +
                $"{window.Size.Width}x{window.Size.Height} " +
                $"logical={window.LogicalSize.Width}x{window.LogicalSize.Height} " +
                $"scale={window.DisplayScale:0.###}");
        }
    }
}
