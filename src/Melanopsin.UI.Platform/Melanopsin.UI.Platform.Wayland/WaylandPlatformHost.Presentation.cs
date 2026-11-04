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
        foreach (var window in _windows.Values) {
            if (window.PendingFrame.IsOccupied) {
                _ = TryPresent(window.Id);
            }
        }
    }

    private bool TryPresent(WindowId windowId) {
        if (!_windows.TryGetValue(windowId, out var window)) {
            return false;
        }

        var pending = window.PendingFrame;
        if (!pending.IsOccupied) {
            _pendingFrameTimestamps.Remove(windowId);
            return false;
        }

        if (!pending.IsLeaseValid) {
            DropStalePendingFrame(windowId, window);
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
                    $"frame queued behind callback {windowId}: " +
                    $"{Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:0.00}ms");
            }

            return false;
        }

        if (!window.CanPresent(size)) {
            return false;
        }

        var lease = pending.Frame;
        var surface = lease.Surface;
        if (lease.VisualAccessibility == default &&
            surface is INativePresentationSurface nativeSurface) {
            ReleasePreviousNativeSurface(window, nativeSurface);
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
                presented = nativeSurface.TryPresentNative(window.Handle);
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
                window.NativePresentationSurface = nativeSurface;
                _connection.FlushChecked();
                pending.Clear();
                _pendingFrameTimestamps.Remove(windowId);
                return true;
            }

            // EGL is unavailable for this backend/window. Drop the callback
            // before falling back to wl_shm so the next attempt can proceed.
            window.CancelFrameCallback();
            _frameCallbackTimestamps.Remove(windowId);
            ReleaseFailedNativePresentation(window, nativeSurface);
        }

        if (surface is not ICpuReadableSurface cpuSurface) {
            pending.Clear();
            _pendingFrameTimestamps.Remove(windowId);
            Trace("frame has no CPU-readable fallback");
            return false;
        }

        window.ConfigureBuffer(pending.Width, pending.Height);
        if (!window.TryAcquireBuffer(
                pending.Width,
                pending.Height,
                surface.Stride,
                out var buffer) ||
            buffer is null) {
            Trace("shm buffer unavailable");
            return false;
        }

        if (!buffer.TryCopyPixels(
                cpuSurface,
                lease.Damage,
                lease.VisualAccessibility)) {
            buffer.ReleaseReservation();
            Trace("frame pixels unavailable");
            return false;
        }

        if (!window.ArmFrameCallback(RetryPendingFrames)) {
            buffer.ReleaseReservation();
            Trace("shm frame callback already pending");
            return false;
        }

        var cpuPresentStart = Stopwatch.GetTimestamp();
        try {
            buffer.Commit();
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

        ReleaseNativeSurface(window);
        pending.Clear();
        _pendingFrameTimestamps.Remove(windowId);
        return true;
    }

    private static void ReleasePreviousNativeSurface(
        WaylandWindow window,
        INativePresentationSurface next) {
        if (window.NativePresentationSurface is { } previous &&
            !ReferenceEquals(previous, next)) {
            previous.ReleaseNativeTarget(window.Handle);
            window.NativePresentationSurface = null;
        }
    }

    private static void ReleaseNativeSurface(WaylandWindow window) {
        if (window.NativePresentationSurface is { } nativeSurface) {
            nativeSurface.ReleaseNativeTarget(window.Handle);
        }

        window.NativePresentationSurface = null;
    }

    private static void ReleaseFailedNativePresentation(
        WaylandWindow window,
        INativePresentationSurface attempted) {
        if (!ReferenceEquals(window.NativePresentationSurface, attempted)) {
            attempted.ReleaseNativeTarget(window.Handle);
        }

        ReleaseNativeSurface(window);
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
        var pending = window.PendingFrame;
        if (!pending.IsOccupied) {
            _pendingFrameTimestamps.Remove(windowId);
            return;
        }

        if (pending.IsLeaseValid &&
            IsPendingFrameCurrent(pending, window)) {
            return;
        }

        var width = pending.Width;
        var height = pending.Height;
        var logicalWidth = pending.LogicalWidth;
        var logicalHeight = pending.LogicalHeight;
        var displayScale = pending.DisplayScale;
        var leaseWasValid = pending.IsLeaseValid;
        pending.Clear();
        _pendingFrameTimestamps.Remove(windowId);
        if (TracePresentation) {
            var reason = leaseWasValid ? "dimensions changed" : "lease invalidated";
            Console.Error.WriteLine(
                $"[wayland] dropped stale pending frame {windowId} " +
                $"{width}x{height} " +
                $"logical={logicalWidth}x{logicalHeight} " +
                $"scale={displayScale:0.###} ({reason}) for " +
                $"{window.Size.Width}x{window.Size.Height} " +
                $"logical={window.LogicalSize.Width}x{window.LogicalSize.Height} " +
                $"scale={window.DisplayScale:0.###}");
        }
    }
}
