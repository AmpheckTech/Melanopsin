using System.ComponentModel;
using System.Runtime.InteropServices;
using Melanopsin.UI.Input;
using Melanopsin.UI.Platform.Win32.Input;
using Melanopsin.UI.Platform.Win32.Windowing;

namespace Melanopsin.UI.Platform.Win32;

/// <summary>
/// Opens native Win32 windows and presents backend-neutral BGRA frames with GDI.
/// </summary>
public sealed class Win32PlatformHost : IPlatformWindowHost {
    private static int _presenterSequence;

    private readonly Win32WindowRegistry _windows = new();
    private readonly Win32WindowEventQueue _events = new();
    private readonly Win32Native.WindowProcedure _windowProcedure;
    private readonly IntPtr _instance;
    private readonly string _className;
    private readonly bool _timerPeriodSet;
    private bool _classRegistered;
    private bool _disposed;
    private Exception? _cleanupFailure;

    public Win32PlatformHost() {
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException(
                "Win32PlatformHost can only run on Windows.");
        }

        _instance = Win32Native.GetModuleHandleW(null);
        if (_instance == IntPtr.Zero) {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetModuleHandleW failed.");
        }

        var awarenessSet = Win32Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        if (!awarenessSet) {
            Win32Native.SetProcessDPIAware();
        }

        var dpi = Win32Native.GetDpiForSystem();
        DisplayScale = dpi > 0 ? dpi / 96f : 1f;
        _timerPeriodSet = Win32Native.timeBeginPeriod(1) == 0;
        _className = $"Melanopsin.UI.{Environment.ProcessId}." +
            Interlocked.Increment(ref _presenterSequence);
        _windowProcedure = new Win32WindowMessageRouter(
            _windows,
            _events).WindowProcedure;
        RegisterWindowClass();
    }

    public int WindowCount => _windows.Count;

    public float DisplayScale { get; }

    /// <summary>
    /// The failure observed during the most recent disposal. Disposal records
    /// this value instead of throwing so every native resource is released.
    /// </summary>
    public Exception? LastCleanupFailure => Volatile.Read(ref _cleanupFailure);

    public void Present(WindowDescriptor windowDescriptor, RenderFrame frame) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);

        var created = false;
        if (!_windows.TryGet(windowDescriptor.Id, out var window)) {
            window = CreateWindow(windowDescriptor);
            _windows.Add(window);
            created = true;
        }

        if (created) {
            Win32Native.ShowWindow(window.Handle, Win32Native.SwShow);
            Win32Native.UpdateWindow(window.Handle);
            if (Win32Native.GetClientRect(window.Handle, out var clientRect)) {
                var clientSize = new PixelSize(
                    Math.Max(1, clientRect.Right - clientRect.Left),
                    Math.Max(1, clientRect.Bottom - clientRect.Top));
                if (window.Size != clientSize) {
                    window.UpdateSize(clientSize);
                    _events.QueueResized(window, clientSize);
                }
            }
        }

        window.Present(frame);
    }

    public void PollEvents() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (Win32Native.PeekMessageW(
                   out var message,
                   IntPtr.Zero,
                   filterMin: 0,
                   filterMax: 0,
                   Win32Native.PmRemove)) {
            Win32Native.TranslateMessage(ref message);
            Win32Native.DispatchMessageW(ref message);
        }
    }

    public bool TryDequeueEvent(out WindowEvent windowEvent) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PollEvents();
        return _events.TryDequeue(out windowEvent);
    }

    public void WaitForEvent(int timeoutMilliseconds) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ = Win32Native.MsgWaitForMultipleObjectsEx(
            handleCount: 0,
            IntPtr.Zero,
            timeoutMilliseconds: (uint)Math.Clamp(
                timeoutMilliseconds,
                0,
                int.MaxValue),
            wakeMask: 0x04FF,
            flags: 0);
    }

    public void CloseWindow(WindowId windowId) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_windows.Remove(windowId, out var window)) {
            return;
        }

        window.Dispose();
        Win32Native.DestroyWindow(window.Handle);
    }

    public void SetCursor(WindowId windowId, CursorKind cursor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_windows.TryGet(windowId, out var window)) {
            return;
        }

        window.SetCursor(cursor);
        if (window.IsTrackingMouse) {
            _ = Win32Native.SetCursor(window.CursorHandle);
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        List<Exception>? failures = null;
        CaptureCleanupFailure(ref failures, _windows.CloseAll);
        CaptureCleanupFailure(ref failures, _events.Clear);
        if (_timerPeriodSet) {
            CaptureCleanupFailure(
                ref failures,
                () => _ = Win32Native.timeEndPeriod(1));
        }

        if (_classRegistered) {
            try {
                CaptureCleanupFailure(
                    ref failures,
                    () => _ = Win32Native.UnregisterClassW(
                        _className,
                        _instance));
            } finally {
                _classRegistered = false;
            }
        }

        Volatile.Write(
            ref _cleanupFailure,
            failures is null
                ? null
                : new AggregateException(
                    "The Win32 platform host failed to dispose cleanly.",
                    failures));
    }

    private void RegisterWindowClass() {
        var windowClass = new WndClassEx {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            Instance = _instance,
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(_windowProcedure),
            Cursor = Win32Native.LoadCursorW(IntPtr.Zero, new IntPtr(Win32Native.IdcArrow)),
            Background = new IntPtr(1),
            ClassName = _className,
        };
        var atom = Win32Native.RegisterClassExW(ref windowClass);
        if (atom == 0) {
            var error = Marshal.GetLastWin32Error();
            if (error != 1410) {
                throw new Win32Exception(error, "RegisterClassExW failed.");
            }
        }

        _classRegistered = true;
    }

    private Win32Window CreateWindow(WindowDescriptor windowDescriptor) {
        var rectangle = new Rect {
            Left = 0,
            Top = 0,
            Right = windowDescriptor.Size.Width,
            Bottom = windowDescriptor.Size.Height,
        };
        Win32Native.AdjustWindowRectEx(
            ref rectangle,
            Win32Native.WsOverlappedWindow,
            menu: false,
            extendedStyle: 0);
        var outerWidth = rectangle.Right - rectangle.Left;
        var outerHeight = rectangle.Bottom - rectangle.Top;
        var (x, y) = FindWindowPosition(outerWidth, outerHeight);
        var handle = Win32Native.CreateWindowExW(
            extendedStyle: 0,
            _className,
            windowDescriptor.Title,
            Win32Native.WsOverlappedWindow,
            x,
            y,
            outerWidth,
            outerHeight,
            parent: IntPtr.Zero,
            menu: IntPtr.Zero,
            _instance,
            parameter: IntPtr.Zero);
        if (handle == IntPtr.Zero) {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW failed.");
        }

        var window = new Win32Window(handle, windowDescriptor);
        _ = window.UpdatePosition(x, y);
        return window;
    }

    private (int X, int Y) FindWindowPosition(
        int width,
        int height) {
        var workArea = new Rect();
        if (!Win32Native.SystemParametersInfoW(
                Win32Native.SpiGetWorkArea,
                0,
                ref workArea,
                0)) {
            workArea = Win32WindowPlacement.CreateFallbackWorkArea(width, height);
        }

        var occupied = new List<Rect>(_windows.Count);
        foreach (var window in _windows.Windows) {
            if (Win32Native.GetWindowRect(window.Handle, out var rectangle)) {
                occupied.Add(rectangle);
            }
        }

        return Win32WindowPlacement.Find(
            workArea,
            occupied,
            width,
            height,
            _windows.Count);
    }

    private static void CaptureCleanupFailure(
        ref List<Exception>? failures,
        Action action) {
        try {
            action();
        } catch (Exception exception) {
            (failures ??= []).Add(exception);
        }
    }
}
