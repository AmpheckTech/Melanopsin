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
public sealed class WaylandPlatformHost : IPlatformWindowHost {
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
            _connection.SeatCapabilitiesChanged += OnSeatCapabilitiesChanged;
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

    public void CloseWindow(WindowId windowId) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_windows.Remove(windowId, out var window)) {
            return;
        }

        _windowsByHandle.Remove(window.Handle);
        _pendingFrames.Remove(windowId);
        _configureTimestamps.Remove(windowId);
        _pendingFrameTimestamps.Remove(windowId);
        _frameCallbackTimestamps.Remove(windowId);
        ReleaseWindowGpuSurface(window);
        window.Dispose();
        UpdateDisplayScale();
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
        _connection.SeatCapabilitiesChanged -= OnSeatCapabilitiesChanged;
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
            Volatile.Write(
                ref _cleanupFailure,
                new InvalidOperationException(_nativeError));
        }
    }

    private WaylandWindow CreateWindow(WindowDescriptor descriptor) {
        var scale = DisplayScale;
        var logicalSize = new PixelSize(
            ToLogicalDimension(descriptor.Size.Width, scale),
            ToLogicalDimension(descriptor.Size.Height, scale));
        var window = new WaylandWindow(
            descriptor.Id,
            _connection,
            descriptor.Title,
            descriptor.Size,
            logicalSize,
            scale,
            HandleWindowConfigured,
            HandleWindowCloseRequested,
            RetryPendingFrames);
        _windows.Add(window.Id, window);
        _windowsByHandle.Add(window.Handle, window);

        try {
            var decoration = _decorationContext.Decorate(
                window,
                window.Surface,
                descriptor.Title,
                logicalSize);
            window.AttachDecoration(decoration);
            _connection.FlushChecked();
        } catch {
            _windows.Remove(window.Id);
            _windowsByHandle.Remove(window.Handle);
            window.Dispose();
            throw;
        }

        return window;
    }

    private WaylandWindow GetOrCreateWindow(WindowDescriptor descriptor) {
        if (!_windows.TryGetValue(descriptor.Id, out var window)) {
            return CreateWindow(descriptor);
        }

        if (!string.Equals(window.Title, descriptor.Title, StringComparison.Ordinal)) {
            window.SetTitle(descriptor.Title);
        }

        return window;
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

    private void HandleWindowConfigured(
        WaylandWindow window,
        bool scaleChanged,
        bool sizeChanged,
        bool requiresPresent) {
        UpdateDisplayScale();
        DropStalePendingFrame(window.Id, window);
        if (TracePresentation) {
            _configureTimestamps[window.Id] = Stopwatch.GetTimestamp();
            Console.Error.WriteLine(
                $"[wayland] configure window={window.Size.Width}x{window.Size.Height} " +
                $"logical={window.LogicalSize.Width}x{window.LogicalSize.Height} " +
                $"scale={window.DisplayScale:0.###} " +
                $"sizeChanged={sizeChanged} scaleChanged={scaleChanged} " +
                $"requiresPresent={requiresPresent}");
        }

        if (sizeChanged) {
            _events.QueueResized(window);
        }

        if (scaleChanged) {
            _events.QueueDpiChanged(window);
        }

        if (requiresPresent) {
            // A configure that needs a real buffer commit must also make the UI
            // dirty. Do not rely on resize/DPI lifecycle events to do this:
            // Window.Resize may be a no-op when the descriptor already matches,
            // and a recovery configure can arrive with no geometry change.
            _events.QueueRenderRequested(window);
        }

        if (!window.IsShown) {
            window.IsShown = true;
            _events.QueueShown(window);
        }
    }

    private void HandleWindowCloseRequested(WaylandWindow window) {
        _events.QueueCloseRequested(window);
    }

    private void HandleWindowPointerEnter(
        WaylandWindow window,
        uint serial,
        double x,
        double y) {
        _pointerWindow = window;
        _pointerX = x;
        _pointerY = y;
        QueuePointer(window, PointerPhase.Enter, MouseButtonKind.None, _pointerTime);
    }

    private void HandleWindowPointerLeave(
        WaylandWindow window,
        uint serial) {
        QueuePointer(window, PointerPhase.Leave, MouseButtonKind.None, _pointerTime);
        if (ReferenceEquals(_pointerWindow, window)) {
            _pointerWindow = null;
        }
    }

    private void HandlePointerMotion(double x, double y, uint time) {
        if (_pointerWindow is not { } window) {
            return;
        }

        _pointerX = x;
        _pointerY = y;
        _pointerTime = time;
        QueuePointer(window, PointerPhase.Move, MouseButtonKind.None, time);
    }

    private void HandlePointerButton(
        uint button,
        WlPointer.ButtonStateEnum state,
        uint time) {
        if (_pointerWindow is not { } window) {
            return;
        }

        var mouseButton = button switch {
            ButtonLeft => MouseButtonKind.Left,
            ButtonRight => MouseButtonKind.Right,
            ButtonMiddle => MouseButtonKind.Middle,
            _ => MouseButtonKind.None,
        };
        if (mouseButton == MouseButtonKind.None) {
            return;
        }

        _pointerTime = time;
        QueuePointer(
            window,
            state == WlPointer.ButtonStateEnum.Pressed
                ? PointerPhase.Down
                : PointerPhase.Up,
            mouseButton,
            time);
    }

    private void HandlePointerAxis(
        WlPointer.AxisEnum axis,
        double value) {
        if (axis == WlPointer.AxisEnum.HorizontalScroll) {
            _axisHorizontal += value;
        } else if (axis == WlPointer.AxisEnum.VerticalScroll) {
            _axisVertical += value;
        } else {
            return;
        }

        _axisPending = true;
    }

    private void HandlePointerAxisValue120(
        WlPointer.AxisEnum axis,
        int value120) {
        if (axis == WlPointer.AxisEnum.HorizontalScroll) {
            _axisHorizontal120 += value120;
        } else if (axis == WlPointer.AxisEnum.VerticalScroll) {
            _axisVertical120 += value120;
        } else {
            return;
        }

        _axisPending = true;
    }

    private void HandlePointerFrame() {
        if (_axisPending && _pointerWindow is { } window) {
            // Wayland scroll values are positive toward the end of the
            // document. UI wheel deltas use positive values for up/left.
            var horizontalDelta = _axisHorizontal120 != 0
                ? -_axisHorizontal120
                : (int)MathF.Round((float)-_axisHorizontal * 8);
            var verticalDelta = _axisVertical120 != 0
                ? -_axisVertical120
                : (int)MathF.Round((float)-_axisVertical * 8);
            _events.QueueScroll(
                window,
                verticalDelta,
                horizontalDelta,
                _pointerTime);
        }

        _axisPending = false;
        _axisHorizontal = 0;
        _axisVertical = 0;
        _axisHorizontal120 = 0;
        _axisVertical120 = 0;
    }

    private void QueuePointer(
        WaylandWindow window,
        PointerPhase phase,
        MouseButtonKind button,
        uint time) {
        window.PointerX = (float)_pointerX * window.DisplayScale;
        window.PointerY = (float)_pointerY * window.DisplayScale;
        _events.QueuePointer(window, phase, button, time);
    }

    private void HandleKeyboardEnter(
        uint serial,
        WlSurface? surface) {
        var window = FindWindow(surface);
        _keyboardWindow = window;
        if (window is not null) {
            window.IsFocused = true;
            _events.QueueFocus(window, focused: true);
        }
    }

    private void HandleKeyboardLeave(
        uint serial,
        WlSurface? surface) {
        var window = FindWindow(surface);
        if (window is not null) {
            window.IsFocused = false;
            _events.QueueFocus(window, focused: false);
        }

        if (ReferenceEquals(_keyboardWindow, window)) {
            _keyboardWindow = null;
        }
    }

    private void HandleKeyboardKey(
        uint time,
        uint key,
        WlKeyboard.KeyStateEnum state) {
        if (_keyboardWindow is not { } window) {
            return;
        }

        var phase = state == WlKeyboard.KeyStateEnum.Released
            ? KeyPhase.Up
            : KeyPhase.Down;
        var keysym = _xkbState?.GetKeySymbol(key, phase) ?? 0;
        var text = phase == KeyPhase.Down
            ? _xkbState?.GetText(key)
            : null;
        _events.QueueKey(
            window,
            WaylandKeyMapper.MapKeySym(keysym),
            phase,
            _xkbState?.IsModifierActive("Shift") == true,
            _xkbState?.IsModifierActive("Control") == true,
            _xkbState?.IsModifierActive("Mod1") == true,
            _xkbState?.IsModifierActive("Mod4") == true,
            text,
            time);
    }

    private void HandleKeyboardModifiers(
        uint depressed,
        uint latched,
        uint locked,
        uint group) {
        _xkbState?.UpdateModifiers(
            depressed,
            latched,
            locked,
            group);
    }

    private void HandleKeymap(int fd, uint size, bool valid) {
        _xkbState?.Dispose();
        _xkbState = null;
        if (!valid) {
            CloseFd(fd);
            return;
        }

        try {
            _xkbState = new WaylandXkbState(fd, size);
        } catch {
            // Input remains usable through the fallback key mapper. A broken
            // keymap should not take down the presentation loop.
            _xkbState = null;
        }
    }

    private void OnSeatCapabilitiesChanged(WlSeat.CapabilityEnum capabilities) {
        UpdateSeatDevices(capabilities);
    }

    private void UpdateSeatDevices(WlSeat.CapabilityEnum capabilities) {
        var wantsPointer =
            (capabilities & WlSeat.CapabilityEnum.Pointer) != 0;
        var wantsKeyboard =
            (capabilities & WlSeat.CapabilityEnum.Keyboard) != 0;

        if (wantsPointer &&
            _pointer is null &&
            _connection.Seat is { } pointerSeat) {
            var pointer = pointerSeat.GetPointer(
                new PointerListener(this),
                _connection.Queue);
            _pointer = pointer;
            _cursorShapeDevice = _connection.CursorShapeManager?.GetPointer(
                pointer,
                _cursorShapeListener,
                _connection.Queue);
        } else if (!wantsPointer) {
            ReleasePointer();
        }

        if (wantsKeyboard &&
            _keyboard is null &&
            _connection.Seat is { } keyboardSeat) {
            _keyboard = keyboardSeat.GetKeyboard(
                new KeyboardListener(this),
                _connection.Queue);
        } else if (!wantsKeyboard) {
            ReleaseKeyboard();
        }
    }

    private void ReleaseInputDevices() {
        ReleasePointer();
        ReleaseKeyboard();
        _xkbState?.Dispose();
        _xkbState = null;
    }

    private void ReleasePointer() {
        _pointerWindow = null;
        _pointerEnterSerial = 0;
        ResetPointerFrame();
        ReleaseCursorShapeDevice();
        if (_pointer is null) {
            return;
        }

        if (_pointer.IsReleaseAvailable) {
            _pointer.Release();
        }

        _pointer.Dispose();
        _pointer = null;
    }

    private void ReleaseCursorShapeDevice() {
        if (_cursorShapeDevice is null) {
            return;
        }

        if (!_cursorShapeDevice.IsDisposed) {
            _cursorShapeDevice.Destroy();
        }

        _cursorShapeDevice.Dispose();
        _cursorShapeDevice = null;
    }

    private void ApplyCursorShape() {
        if (_cursorShapeDevice is null || _pointerEnterSerial == 0) {
            return;
        }

        _cursorShapeDevice.SetShape(
            _pointerEnterSerial,
            MapCursorShape(_cursor));
        _connection.FlushChecked();
    }

    private static WpCursorShapeDeviceV1.ShapeEnum MapCursorShape(
        CursorKind cursor) {
        return cursor switch {
            CursorKind.ContextMenu => WpCursorShapeDeviceV1.ShapeEnum.ContextMenu,
            CursorKind.Help => WpCursorShapeDeviceV1.ShapeEnum.Help,
            CursorKind.Pointer => WpCursorShapeDeviceV1.ShapeEnum.Pointer,
            CursorKind.Progress => WpCursorShapeDeviceV1.ShapeEnum.Progress,
            CursorKind.Wait => WpCursorShapeDeviceV1.ShapeEnum.Wait,
            CursorKind.Cell => WpCursorShapeDeviceV1.ShapeEnum.Cell,
            CursorKind.Crosshair => WpCursorShapeDeviceV1.ShapeEnum.Crosshair,
            CursorKind.Text => WpCursorShapeDeviceV1.ShapeEnum.Text,
            CursorKind.VerticalText => WpCursorShapeDeviceV1.ShapeEnum.VerticalText,
            CursorKind.Alias => WpCursorShapeDeviceV1.ShapeEnum.Alias,
            CursorKind.Copy => WpCursorShapeDeviceV1.ShapeEnum.Copy,
            CursorKind.Move => WpCursorShapeDeviceV1.ShapeEnum.Move,
            CursorKind.NoDrop => WpCursorShapeDeviceV1.ShapeEnum.NoDrop,
            CursorKind.NotAllowed => WpCursorShapeDeviceV1.ShapeEnum.NotAllowed,
            CursorKind.Grab => WpCursorShapeDeviceV1.ShapeEnum.Grab,
            CursorKind.Grabbing => WpCursorShapeDeviceV1.ShapeEnum.Grabbing,
            CursorKind.EResize => WpCursorShapeDeviceV1.ShapeEnum.EResize,
            CursorKind.NResize => WpCursorShapeDeviceV1.ShapeEnum.NResize,
            CursorKind.NeResize => WpCursorShapeDeviceV1.ShapeEnum.NeResize,
            CursorKind.NwResize => WpCursorShapeDeviceV1.ShapeEnum.NwResize,
            CursorKind.SResize => WpCursorShapeDeviceV1.ShapeEnum.SResize,
            CursorKind.SeResize => WpCursorShapeDeviceV1.ShapeEnum.SeResize,
            CursorKind.SwResize => WpCursorShapeDeviceV1.ShapeEnum.SwResize,
            CursorKind.WResize => WpCursorShapeDeviceV1.ShapeEnum.WResize,
            CursorKind.EwResize => WpCursorShapeDeviceV1.ShapeEnum.EwResize,
            CursorKind.NsResize => WpCursorShapeDeviceV1.ShapeEnum.NsResize,
            CursorKind.NeswResize => WpCursorShapeDeviceV1.ShapeEnum.NeswResize,
            CursorKind.NwseResize => WpCursorShapeDeviceV1.ShapeEnum.NwseResize,
            CursorKind.ColResize => WpCursorShapeDeviceV1.ShapeEnum.ColResize,
            CursorKind.RowResize => WpCursorShapeDeviceV1.ShapeEnum.RowResize,
            CursorKind.AllScroll => WpCursorShapeDeviceV1.ShapeEnum.AllScroll,
            CursorKind.ZoomIn => WpCursorShapeDeviceV1.ShapeEnum.ZoomIn,
            CursorKind.ZoomOut => WpCursorShapeDeviceV1.ShapeEnum.ZoomOut,
            CursorKind.DndAsk => WpCursorShapeDeviceV1.ShapeEnum.DndAsk,
            CursorKind.AllResize => WpCursorShapeDeviceV1.ShapeEnum.AllResize,
            _ => WpCursorShapeDeviceV1.ShapeEnum.Default,
        };
    }

    private void ReleaseKeyboard() {
        _keyboardWindow = null;
        if (_keyboard is null) {
            return;
        }

        if (_keyboard.IsReleaseAvailable) {
            _keyboard.Release();
        }

        _keyboard.Dispose();
        _keyboard = null;
    }

    private void ResetPointerFrame() {
        _axisPending = false;
        _axisHorizontal = 0;
        _axisVertical = 0;
        _axisHorizontal120 = 0;
        _axisVertical120 = 0;
    }

    private WaylandWindow? FindWindow(WlSurface? surface) {
        return surface is not null &&
            _windowsByHandle.TryGetValue(surface.Handle, out var window)
            ? window
            : null;
    }

    private static void CloseFd(int fd) {
        if (fd >= 0) {
            _ = LibcClose(fd);
        }
    }

    [DllImport("libc", EntryPoint = "close")]
    private static extern int LibcClose(int fd);

    private static WaylandPendingFrame CreatePendingFrame(
        WaylandWindow window,
        WindowDescriptor descriptor,
        RenderFrame frame) {
        var byteCount = checked(frame.Stride * frame.Descriptor.Size.Height);
        if (frame.GpuSurface is null && frame.Pixels.Length < byteCount) {
            throw new ArgumentException(
                "The frame pixel buffer is smaller than its stride and height.",
                nameof(frame));
        }

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

    private void UpdateDisplayScale() {
        var scale = 1f;
        foreach (var window in _windows.Values) {
            scale = Math.Max(scale, window.DisplayScale);
        }

        Volatile.Write(ref _displayScale, NormalizeScale(scale));
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

    private static int ToLogicalDimension(int physical, float scale) {
        return Math.Max(1, (int)MathF.Round(physical / NormalizeScale(scale)));
    }

    private sealed class PointerListener : WlPointer.Listener {
        private readonly WaylandPlatformHost _host;

        public PointerListener(WaylandPlatformHost host) {
            _host = host;
        }

        protected override void Enter(
            WlPointer eventSender,
            uint serial,
            WlSurface? surface,
            WlFixed surfaceX,
            WlFixed surfaceY) {
            _host._pointerEnterSerial = serial;
            if (_host.FindWindow(surface) is { } window) {
                _host.HandleWindowPointerEnter(
                    window,
                    serial,
                    (double)surfaceX,
                    (double)surfaceY);
            }
        }

        protected override void Leave(
            WlPointer eventSender,
            uint serial,
            WlSurface? surface) {
            if (_host.FindWindow(surface) is { } window) {
                _host.HandleWindowPointerLeave(window, serial);
            }
        }

        protected override void Motion(
            WlPointer eventSender,
            uint time,
            WlFixed surfaceX,
            WlFixed surfaceY) {
            _host.HandlePointerMotion(
                (double)surfaceX,
                (double)surfaceY,
                time);
        }

        protected override void Button(
            WlPointer eventSender,
            uint serial,
            uint time,
            uint button,
            WlPointer.ButtonStateEnum state) {
            _host.HandlePointerButton(button, state, time);
        }

        protected override void Axis(
            WlPointer eventSender,
            uint time,
            WlPointer.AxisEnum axis,
            WlFixed value) {
            _host._pointerTime = time;
            _host.HandlePointerAxis(axis, (double)value);
        }

        protected override void Frame(WlPointer eventSender) {
            _host.HandlePointerFrame();
        }

        protected override void AxisValue120(
            WlPointer eventSender,
            WlPointer.AxisEnum axis,
            int value120) {
            _host.HandlePointerAxisValue120(axis, value120);
        }
    }

    private sealed class KeyboardListener : WlKeyboard.Listener {
        private readonly WaylandPlatformHost _host;

        public KeyboardListener(WaylandPlatformHost host) {
            _host = host;
        }

        protected override void Keymap(
            WlKeyboard eventSender,
            WlKeyboard.KeymapFormatEnum format,
            WaylandFd fd,
            uint size) {
            var rawFd = fd.Consume();
            _host.HandleKeymap(
                rawFd,
                size,
                format == WlKeyboard.KeymapFormatEnum.XkbV1);
        }

        protected override void Enter(
            WlKeyboard eventSender,
            uint serial,
            WlSurface? surface,
            ReadOnlySpan<byte> keys) {
            _host.HandleKeyboardEnter(serial, surface);
        }

        protected override void Leave(
            WlKeyboard eventSender,
            uint serial,
            WlSurface? surface) {
            _host.HandleKeyboardLeave(serial, surface);
        }

        protected override void Key(
            WlKeyboard eventSender,
            uint serial,
            uint time,
            uint key,
            WlKeyboard.KeyStateEnum state) {
            _host.HandleKeyboardKey(time, key, state);
        }

        protected override void Modifiers(
            WlKeyboard eventSender,
            uint serial,
            uint modsDepressed,
            uint modsLatched,
            uint modsLocked,
            uint group) {
            _host.HandleKeyboardModifiers(
                modsDepressed,
                modsLatched,
                modsLocked,
                group);
        }

        protected override void RepeatInfo(
            WlKeyboard eventSender,
            int rate,
            int delay) {
        }
    }

    private sealed class CursorShapeListener : WpCursorShapeDeviceV1.Listener {
    }
}
