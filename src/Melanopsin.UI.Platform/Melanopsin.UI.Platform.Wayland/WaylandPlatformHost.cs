using System.Diagnostics;
using System.Runtime.InteropServices;
using Melanopsin.UI;
using Melanopsin.UI.Input;
using Melanopsin.UI.Platform.Wayland.Input;
using NWayland;
using NWayland.Protocols.CursorShapeV1;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

/// <summary>
/// Hosts one compositor-decorated Wayland window per UI window and presents
/// BGRA frames through <c>wl_shm</c> or an EGL presentation surface.
/// </summary>
public sealed partial class WaylandPlatformHost : IPlatformWindowHost {
    private const uint ButtonLeft = 0x110;
    private const uint ButtonRight = 0x111;
    private const uint ButtonMiddle = 0x112;
    private static readonly bool TracePresentation =
        string.Equals(
            Environment.GetEnvironmentVariable("MELANOPSIN_WAYLAND_TRACE"),
            "1",
            StringComparison.Ordinal);

    private readonly WaylandConnection _connection;
    private readonly WaylandLibDecorContext _decorationContext;
    private readonly Dictionary<WindowId, WaylandWindow> _windows = [];
    private readonly Dictionary<IntPtr, WaylandWindow> _windowsByHandle = [];
    private readonly Dictionary<WindowId, WaylandPendingFrame> _pendingFrames = [];
    private readonly Dictionary<WindowId, long> _configureTimestamps = [];
    private readonly Dictionary<WindowId, long> _pendingFrameTimestamps = [];
    private readonly Dictionary<WindowId, long> _frameCallbackTimestamps = [];
    private readonly WaylandWindowEventQueue _events = new();
    private WlPointer? _pointer;
    private WpCursorShapeDeviceV1? _cursorShapeDevice;
    private readonly CursorShapeListener _cursorShapeListener = new();
    private WlKeyboard? _keyboard;
    private WaylandXkbState? _xkbState;
    private WaylandWindow? _pointerWindow;
    private WaylandWindow? _keyboardWindow;
    private double _pointerX;
    private double _pointerY;
    private double _axisHorizontal;
    private double _axisVertical;
    private int _axisHorizontal120;
    private int _axisVertical120;
    private bool _axisPending;
    private uint _pointerTime;
    private uint _pointerEnterSerial;
    private CursorKind _cursor = CursorKind.Auto;
    private float _displayScale = 1f;
    private string? _nativeError;
    private bool _disposed;
    private Exception? _cleanupFailure;

    public WaylandPlatformHost(string? displayName = null) {
        if (!OperatingSystem.IsLinux()) {
            throw new PlatformNotSupportedException(
                "WaylandPlatformHost can only run on Linux.");
        }

        _connection = new WaylandConnection(displayName);
        try {
            _decorationContext = new WaylandLibDecorContext(
                _connection.Display.Handle,
                HandleError);
        } catch {
            _connection.Dispose();
            throw;
        }

        try {
            _connection.SeatCapabilitiesChanged += UpdateSeatDevices;
            UpdateSeatDevices(_connection.SeatCapabilities);
        } catch {
            Dispose();
            throw;
        }
    }

    public int WindowCount => _windows.Count;

    public float DisplayScale => Volatile.Read(ref _displayScale);

    /// <summary>
    /// The <c>wl_display</c> owned by this host. GPU backends can create an
    /// EGL display from this handle without opening a second Wayland
    /// connection.
    /// </summary>
    public IntPtr NativeDisplayHandle => _connection.Display.Handle;

    /// <summary>
    /// The failure observed during disposal. Disposal records this value
    /// instead of throwing, matching the other native platform hosts.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public bool IsPresentationReady(WindowDescriptor windowDescriptor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_windows.TryGetValue(windowDescriptor.Id, out var window)) {
            return true;
        }

        // A compositor configure can arrive before the previous frame reaches
        // the compositor. Use the host window state rather than the caller's
        // descriptor: the descriptor may lag behind the configure event that
        // already changed the surface. A stale buffer must be dropped before
        // applying backpressure, otherwise IsPresentationReady stays false
        // forever and the UI never gets a chance to render the new size.
        DropStalePendingFrame(window.Id, window);

        // A frame may only be rendered after the compositor has released the
        // previous one. Keeping the pending-frame check here as well as in
        // TryPresent prevents a configure storm from replacing the same
        // pending frame before it has had a chance to reach the compositor.
        return !_pendingFrames.ContainsKey(window.Id) &&
            window.CanPresent(window.Size);
    }

    public void Present(WindowDescriptor windowDescriptor, RenderFrame frame) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.PixelFormat != RenderPixelFormat.Bgra8888Premultiplied) {
            throw new ArgumentException(
                $"The Wayland presenter requires BGRA8888 frames, got {frame.PixelFormat}.",
                nameof(frame));
        }

        var window = GetOrCreateWindow(windowDescriptor);
        var pending = CreatePendingFrame(window, windowDescriptor, frame);
        if (TracePresentation && _pendingFrames.ContainsKey(window.Id)) {
            Console.Error.WriteLine(
                $"[wayland] replacing pending frame {window.Id} " +
                $"with {pending.Width}x{pending.Height}");
        }

        _pendingFrames[window.Id] = pending;
        _pendingFrameTimestamps[window.Id] = Stopwatch.GetTimestamp();
        if (TracePresentation &&
            _configureTimestamps.Remove(
                window.Id,
                out var configureTimestamp)) {
            Console.Error.WriteLine(
                $"[wayland] frame ready after configure {window.Id}: " +
                $"{Stopwatch.GetElapsedTime(configureTimestamp).TotalMilliseconds:0.00}ms");
        }

        _ = TryPresent(window.Id);
    }

    public bool TryDequeueEvent(out WindowEvent windowEvent) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DispatchPending();
        return _events.TryDequeue(out windowEvent);
    }

    public void WaitForEvent(int timeoutMilliseconds) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Dispatch(timeoutMilliseconds < 0 ? -1 : timeoutMilliseconds);
    }

    public void SetCursor(WindowId windowId, CursorKind cursor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_windows.ContainsKey(windowId) ||
            _pointerWindow?.Id != windowId) {
            return;
        }

        _cursor = cursor;
        ApplyCursorShape();
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _connection.SeatCapabilitiesChanged -= UpdateSeatDevices;
        ReleaseInputDevices();
        foreach (var window in _windows.Values) {
            try {
                ReleaseWindowGpuSurface(window);
                window.Dispose();
            } catch (Exception exception) {
                _cleanupFailure ??= exception;
            }
        }

        _pendingFrames.Clear();
        _configureTimestamps.Clear();
        _pendingFrameTimestamps.Clear();
        _frameCallbackTimestamps.Clear();
        _windowsByHandle.Clear();
        _windows.Clear();
        _events.Clear();

        try {
            _decorationContext.Dispose();
        } catch (Exception exception) {
            _cleanupFailure ??= exception;
        }

        try {
            _connection.Dispose();
        } catch (Exception exception) {
            _cleanupFailure ??= exception;
        }

        if (!string.IsNullOrEmpty(_nativeError)) {
            var nativeFailure = new InvalidOperationException(_nativeError);
            var cleanupFailure = Volatile.Read(ref _cleanupFailure);
            Volatile.Write(
                ref _cleanupFailure,
                cleanupFailure is null
                    ? nativeFailure
                    : new AggregateException(cleanupFailure, nativeFailure));
        }
    }

    private void DispatchPending() {
        Dispatch(0);
    }

    private void Dispatch(int timeoutMilliseconds) {
        var result = _decorationContext.Dispatch(timeoutMilliseconds);
        // -EINTR is a transient interruption. The default queue may still have
        // events ready, so continue with the managed dispatch instead of
        // returning early or treating the interruption as a fatal Wayland error.
        if (result < 0 && result != -4) {
            throw CreateNativeException(
                "libdecor failed while dispatching Wayland events.");
        }

        if (_connection.DispatchPending() < 0) {
            throw CreateNativeException("Wayland failed while dispatching events.");
        }

        _connection.FlushChecked();
        RetryPendingFrames();
    }

    private void HandleError(string message) {
        _nativeError = message;
    }

    private static void Trace(string message) {
        if (TracePresentation) {
            Console.Error.WriteLine($"[wayland] {message}");
        }
    }

    private InvalidOperationException CreateNativeException(string message) {
        return new InvalidOperationException(
            string.IsNullOrEmpty(_nativeError)
                ? message
                : $"{message} {_nativeError}");
    }

    private static float NormalizeScale(float scale) {
        return float.IsFinite(scale) && scale > 0 ? scale : 1f;
    }
}
