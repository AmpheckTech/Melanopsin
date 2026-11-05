using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed unsafe class WaylandLibDecorContext : IDisposable {
    private static readonly LibDecorNative.LibDecorInterface* Interface =
        CreateInterface();

    private static readonly ConcurrentDictionary<IntPtr, WaylandLibDecorContext> Contexts = [];

    private readonly Action<string> _onError;
    private readonly object _lifetimeLock = new();
    private IntPtr _handle;
    private bool _disposed;

    public WaylandLibDecorContext(IntPtr display, Action<string> onError) {
        LibDecorPluginDirectory.PreferCairoBackend();
        _onError = onError;
        _handle = LibDecorNative.New(display, Interface);
        if (_handle == IntPtr.Zero) {
            throw new InvalidOperationException(
                "libdecor could not be initialized for the Wayland display.");
        }

        Contexts[_handle] = this;
    }

    public IntPtr Handle => _handle;

    public int Dispatch(int timeoutMilliseconds) {
        lock (_lifetimeLock) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return LibDecorNative.Dispatch(
                _handle,
                timeoutMilliseconds < 0 ? -1 : timeoutMilliseconds);
        }
    }

    public WaylandDecoration Decorate(
        WaylandWindow window,
        WlSurface surface,
        string title,
        PixelSize initialLogicalSize,
        WaylandDecoration? parent = null) {
        lock (_lifetimeLock) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new WaylandDecoration(
                this,
                window,
                surface,
                title,
                initialLogicalSize,
                parent);
        }
    }

    public void Dispose() {
        IntPtr handle;
        lock (_lifetimeLock) {
            if (_disposed) {
                return;
            }

            _disposed = true;
            handle = _handle;
            _handle = IntPtr.Zero;
        }

        if (handle != IntPtr.Zero) {
            _ = Contexts.TryRemove(handle, out _);
            LibDecorNative.Unref(handle);
        }
    }

    private void ReportError(string text) {
        lock (_lifetimeLock) {
            if (!_disposed) {
                _onError(text);
            }
        }
    }

    private static LibDecorNative.LibDecorInterface* CreateInterface() {
        var @interface = (LibDecorNative.LibDecorInterface*)NativeMemory.Alloc(
            (nuint)sizeof(LibDecorNative.LibDecorInterface));
        *@interface = new LibDecorNative.LibDecorInterface {
            Error = &OnError,
        };
        return @interface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnError(
        IntPtr context,
        LibDecorError error,
        byte* message) {
        if (context == IntPtr.Zero) {
            return;
        }

        try {
            var text = Marshal.PtrToStringUTF8((IntPtr)message) ??
                error.ToString();
            if (Contexts.TryGetValue(context, out var self)) {
                self.ReportError(text);
            }
        } catch {
            // Managed exceptions must never cross an UnmanagedCallersOnly ABI.
        }
    }

    internal enum LibDecorError {
        CompositorIncompatible,
        InvalidFrameConfiguration,
    }
}

internal sealed unsafe class WaylandDecoration : IDisposable {
    private static readonly LibDecorNative.LibDecorFrameInterface* FrameInterface =
        CreateFrameInterface();
    private readonly WaylandLibDecorContext _context;
    private readonly GCHandle _self;
    private IntPtr _frame;
    private bool _disposed;

    public WaylandDecoration(
        WaylandLibDecorContext context,
        WaylandWindow window,
        WlSurface surface,
        string title,
        PixelSize initialLogicalSize,
        WaylandDecoration? parent) {
        _context = context;
        Window = window;
        InitialLogicalSize = initialLogicalSize;
        _self = GCHandle.Alloc(this);
        _frame = LibDecorNative.Decorate(
            context.Handle,
            surface.Handle,
            FrameInterface,
            GCHandle.ToIntPtr(_self));
        if (_frame == IntPtr.Zero) {
            _self.Free();
            throw new InvalidOperationException(
                "libdecor could not decorate the Wayland surface.");
        }

        try {
            LibDecorNative.FrameSetTitle(_frame, title);
            LibDecorNative.FrameSetAppId(_frame, "dev.melanopsin.demo");
            LibDecorNative.FrameSetParent(_frame, parent?.FrameHandle ?? IntPtr.Zero);
            LibDecorNative.FrameMap(_frame);
        } catch {
            Dispose();
            throw;
        }
    }

    public WaylandWindow? Window { get; set; }

    public PixelSize InitialLogicalSize { get; }

    private IntPtr FrameHandle => _frame;

    public void SetTitle(string title) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LibDecorNative.FrameSetTitle(_frame, title);
    }

    public void SetParent(WaylandDecoration? parent) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LibDecorNative.FrameSetParent(_frame, parent?.FrameHandle ?? IntPtr.Zero);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Window = null;
        if (_frame != IntPtr.Zero) {
            LibDecorNative.FrameUnref(_frame);
            _frame = IntPtr.Zero;
        }

        if (_self.IsAllocated) {
            _self.Free();
        }
    }

    private void Configure(IntPtr configuration) {
        if (_disposed) {
            return;
        }

        var width = Window?.LogicalSize.Width ?? InitialLogicalSize.Width;
        var height = Window?.LogicalSize.Height ?? InitialLogicalSize.Height;
        if (LibDecorNative.ConfigurationGetContentSize(
                configuration,
                _frame,
                out var configuredWidth,
                out var configuredHeight) &&
            configuredWidth > 0 &&
            configuredHeight > 0) {
            width = configuredWidth;
            height = configuredHeight;
        }

        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var logicalSize = new PixelSize(width, height);
        var state = LibDecorNative.StateNew(width, height);
        if (state == IntPtr.Zero) {
            return;
        }

        // Update the window before libdecor applies the new geometry. The
        // decoration commit callback is required for synchronous subsurfaces;
        // WaylandWindow forwards whether a real frame must be presented.
        Window?.OnDecorationConfigured(logicalSize);
        try {
            LibDecorNative.FrameCommit(_frame, state, configuration);
        } finally {
            LibDecorNative.StateFree(state);
        }
    }

    private void RequestClose() {
        if (!_disposed) {
            Window?.OnDecorationCloseRequested();
        }
    }

    private void CommitSurface() {
        if (!_disposed) {
            Window?.CommitSurface();
        }
    }

    private static LibDecorNative.LibDecorFrameInterface* CreateFrameInterface() {
        var @interface = (LibDecorNative.LibDecorFrameInterface*)NativeMemory.Alloc(
            (nuint)sizeof(LibDecorNative.LibDecorFrameInterface));
        *@interface = new LibDecorNative.LibDecorFrameInterface {
            Configure = &OnConfigure,
            Close = &OnClose,
            Commit = &OnCommit,
            DismissPopup = &OnDismissPopup,
        };
        return @interface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnConfigure(
        IntPtr frame,
        IntPtr configuration,
        IntPtr userData) {
        try {
            GetDecoration(userData)?.Configure(configuration);
        } catch {
            // Managed exceptions must never cross an UnmanagedCallersOnly ABI.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnClose(IntPtr frame, IntPtr userData) {
        try {
            GetDecoration(userData)?.RequestClose();
        } catch {
            // Managed exceptions must never cross an UnmanagedCallersOnly ABI.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnCommit(IntPtr frame, IntPtr userData) {
        try {
            GetDecoration(userData)?.CommitSurface();
        } catch {
            // Managed exceptions must never cross an UnmanagedCallersOnly ABI.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnDismissPopup(
        IntPtr frame,
        byte* seatName,
        IntPtr userData) {
    }

    private static WaylandDecoration? GetDecoration(IntPtr userData) {
        return userData == IntPtr.Zero
            ? null
            : GCHandle.FromIntPtr(userData).Target as WaylandDecoration;
    }
}

internal static unsafe partial class LibDecorNative {
    private const string LibraryName = "libdecor-0.so.0";

    [StructLayout(LayoutKind.Sequential)]
    internal struct LibDecorInterface {
        internal delegate* unmanaged[Cdecl]<IntPtr, WaylandLibDecorContext.LibDecorError, byte*, void> Error;
        internal IntPtr Reserved0;
        internal IntPtr Reserved1;
        internal IntPtr Reserved2;
        internal IntPtr Reserved3;
        internal IntPtr Reserved4;
        internal IntPtr Reserved5;
        internal IntPtr Reserved6;
        internal IntPtr Reserved7;
        internal IntPtr Reserved8;
        internal IntPtr Reserved9;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LibDecorFrameInterface {
        internal delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> Configure;
        internal delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> Close;
        internal delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> Commit;
        internal delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr, void> DismissPopup;
        internal IntPtr Reserved0;
        internal IntPtr Reserved1;
        internal IntPtr Reserved2;
        internal IntPtr Reserved3;
        internal IntPtr Reserved4;
        internal IntPtr Reserved5;
        internal IntPtr Reserved6;
        internal IntPtr Reserved7;
        internal IntPtr Reserved8;
        internal IntPtr Reserved9;
    }

    [LibraryImport(LibraryName, EntryPoint = "libdecor_new")]
    internal static partial IntPtr New(
        IntPtr display,
        LibDecorInterface* @interface);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_unref")]
    internal static partial void Unref(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_get_fd")]
    internal static partial int GetFd(IntPtr context);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_dispatch")]
    internal static partial int Dispatch(IntPtr context, int timeout);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_decorate")]
    internal static partial IntPtr Decorate(
        IntPtr context,
        IntPtr surface,
        LibDecorFrameInterface* @interface,
        IntPtr userData);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_frame_unref")]
    internal static partial void FrameUnref(IntPtr frame);

    [LibraryImport(
        LibraryName,
        EntryPoint = "libdecor_frame_set_title",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void FrameSetTitle(IntPtr frame, string title);

    [LibraryImport(
        LibraryName,
        EntryPoint = "libdecor_frame_set_app_id",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void FrameSetAppId(IntPtr frame, string appId);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_frame_map")]
    internal static partial void FrameMap(IntPtr frame);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_frame_set_parent")]
    internal static partial void FrameSetParent(IntPtr frame, IntPtr parent);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_frame_commit")]
    internal static partial void FrameCommit(
        IntPtr frame,
        IntPtr state,
        IntPtr configuration);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_state_new")]
    internal static partial IntPtr StateNew(int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "libdecor_state_free")]
    internal static partial void StateFree(IntPtr state);

    [LibraryImport(
        LibraryName,
        EntryPoint = "libdecor_configuration_get_content_size")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool ConfigurationGetContentSize(
        IntPtr configuration,
        IntPtr frame,
        out int width,
        out int height);
}
