using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandNotifications : IPlatformNotifications, IDisposable {
    private readonly NotifyApi? _api;
    private bool _disposed;

    public WaylandNotifications() {
        _api = NotifyApi.TryCreate();
    }

    public bool IsSupported => !_disposed && _api is not null;

    public ValueTask<bool> ShowAsync(
        NotificationOptions notification,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _api is null
            ? ValueTask.FromResult(false)
            : ValueTask.FromResult(_api.Show(notification));
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        _api?.Dispose();
    }

    private sealed class NotifyApi : IDisposable {
        private readonly IntPtr _library;
        private readonly IntPtr _objectLibrary;
        private readonly IntPtr _glibLibrary;
        private readonly notify_init _init;
        private readonly notify_uninit _uninit;
        private readonly notify_notification_new _new;
        private readonly notify_notification_set_app_name _setAppName;
        private readonly notify_notification_show _show;
        private readonly g_object_unref _unref;
        private readonly g_error_free _errorFree;
        private bool _initialized;
        private bool _disposed;

        private NotifyApi(
            IntPtr library,
            IntPtr objectLibrary,
            IntPtr glibLibrary,
            notify_init init,
            notify_uninit uninit,
            notify_notification_new @new,
            notify_notification_set_app_name setAppName,
            notify_notification_show show,
            g_object_unref unref,
            g_error_free errorFree) {
            _library = library;
            _objectLibrary = objectLibrary;
            _glibLibrary = glibLibrary;
            _init = init;
            _uninit = uninit;
            _new = @new;
            _setAppName = setAppName;
            _show = show;
            _unref = unref;
            _errorFree = errorFree;
            _initialized = _init("Melanopsin") != 0;
            if (!_initialized) {
                throw new InvalidOperationException(
                    "libnotify could not be initialized.");
            }
        }

        public static NotifyApi? TryCreate() {
            if (!NativeLibrary.TryLoad("libnotify.so.4", out var library)) {
                return null;
            }

            if (!NativeLibrary.TryLoad(
                    "libgobject-2.0.so.0",
                    out var objectLibrary)) {
                NativeLibrary.Free(library);
                return null;
            }

            if (!NativeLibrary.TryLoad(
                    "libglib-2.0.so.0",
                    out var glibLibrary)) {
                NativeLibrary.Free(objectLibrary);
                NativeLibrary.Free(library);
                return null;
            }

            try {
                return new NotifyApi(
                    library,
                    objectLibrary,
                    glibLibrary,
                    GetExport<notify_init>(library, "notify_init"),
                    GetExport<notify_uninit>(library, "notify_uninit"),
                    GetExport<notify_notification_new>(
                        library,
                        "notify_notification_new"),
                    GetExport<notify_notification_set_app_name>(
                        library,
                        "notify_notification_set_app_name"),
                    GetExport<notify_notification_show>(
                        library,
                        "notify_notification_show"),
                    GetExport<g_object_unref>(
                        objectLibrary,
                        "g_object_unref"),
                    GetExport<g_error_free>(
                        glibLibrary,
                        "g_error_free"));
            } catch {
                NativeLibrary.Free(glibLibrary);
                NativeLibrary.Free(objectLibrary);
                NativeLibrary.Free(library);
                return null;
            }
        }

        public bool Show(NotificationOptions notification) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var handle = _new(notification.Title, notification.Message, null);
            if (handle == IntPtr.Zero) {
                return false;
            }

            try {
                _setAppName(handle, "Melanopsin");
                var shown = _show(handle, out var error) != 0;
                if (!shown && error != IntPtr.Zero) {
                    _errorFree(error);
                }

                return shown;
            } finally {
                _unref(handle);
            }
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            if (_initialized) {
                _uninit();
                _initialized = false;
            }

            NativeLibrary.Free(_glibLibrary);
            NativeLibrary.Free(_objectLibrary);
            NativeLibrary.Free(_library);
        }

        private static T GetExport<T>(IntPtr library, string name)
            where T : Delegate {
            return Marshal.GetDelegateForFunctionPointer<T>(
                NativeLibrary.GetExport(library, name));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int notify_init(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string appName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void notify_uninit();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr notify_notification_new(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string summary,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string body,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? icon);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void notify_notification_set_app_name(
            IntPtr notification,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string appName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int notify_notification_show(
            IntPtr notification,
            out IntPtr error);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void g_object_unref(IntPtr instance);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void g_error_free(IntPtr error);
    }
}
