using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class LibNotifyNative : IDisposable {
    private readonly IntPtr _library;
    private readonly IntPtr _objectLibrary;
    private readonly IntPtr _glibLibrary;
    private bool _initialized;
    private bool _disposed;

    private LibNotifyNative(
        IntPtr library,
        IntPtr objectLibrary,
        IntPtr glibLibrary) {
        _library = library;
        _objectLibrary = objectLibrary;
        _glibLibrary = glibLibrary;
        Init = Export<notify_init>(library, "notify_init");
        Uninit = Export<notify_uninit>(library, "notify_uninit");
        New = Export<notify_notification_new>(library, "notify_notification_new");
        Update = Export<notify_notification_update>(library, "notify_notification_update");
        Show = Export<notify_notification_show>(library, "notify_notification_show");
        SetTimeout = Export<notify_notification_set_timeout>(library, "notify_notification_set_timeout");
        AddAction = Export<notify_notification_add_action>(library, "notify_notification_add_action");
        ClearActions = Export<notify_notification_clear_actions>(library, "notify_notification_clear_actions");
        Close = Export<notify_notification_close>(library, "notify_notification_close");
        GetClosedReason = Export<notify_notification_get_closed_reason>(library, "notify_notification_get_closed_reason");
        GetServerCaps = Export<notify_get_server_caps>(library, "notify_get_server_caps");
        SetAppName = Export<notify_notification_set_app_name>(library, "notify_notification_set_app_name");
        SetHintString = Export<notify_notification_set_hint_string>(library, "notify_notification_set_hint_string");
        SetHintByte = Export<notify_notification_set_hint_byte>(library, "notify_notification_set_hint_byte");
        SignalConnectData = Export<g_signal_connect_data>(objectLibrary, "g_signal_connect_data");
        SignalHandlerDisconnect = Export<g_signal_handler_disconnect>(objectLibrary, "g_signal_handler_disconnect");
        ObjectUnref = Export<g_object_unref>(objectLibrary, "g_object_unref");
        DefaultMainContext = Export<g_main_context_default>(glibLibrary, "g_main_context_default");
        MainContextIteration = Export<g_main_context_iteration>(glibLibrary, "g_main_context_iteration");
        ErrorFree = Export<g_error_free>(glibLibrary, "g_error_free");
        ListFree = Export<g_list_free>(glibLibrary, "g_list_free");
        _initialized = Init("Melanopsin") != 0;
    }

    public notify_init Init { get; }
    public notify_uninit Uninit { get; }
    public notify_notification_new New { get; }
    public notify_notification_update Update { get; }
    public notify_notification_show Show { get; }
    public notify_notification_set_timeout SetTimeout { get; }
    public notify_notification_add_action AddAction { get; }
    public notify_notification_clear_actions ClearActions { get; }
    public notify_notification_close Close { get; }
    public notify_notification_get_closed_reason GetClosedReason { get; }
    public notify_get_server_caps GetServerCaps { get; }
    public notify_notification_set_app_name SetAppName { get; }
    public notify_notification_set_hint_string SetHintString { get; }
    public notify_notification_set_hint_byte SetHintByte { get; }
    public g_signal_connect_data SignalConnectData { get; }
    public g_signal_handler_disconnect SignalHandlerDisconnect { get; }
    public g_object_unref ObjectUnref { get; }
    public g_main_context_default DefaultMainContext { get; }
    public g_main_context_iteration MainContextIteration { get; }
    public g_error_free ErrorFree { get; }
    public g_list_free ListFree { get; }

    public bool IsInitialized => _initialized;

    public static LibNotifyNative? TryCreate() {
        if (!NativeLibrary.TryLoad("libnotify.so.4", out var library)) {
            return null;
        }

        if (!NativeLibrary.TryLoad("libgobject-2.0.so.0", out var objectLibrary)) {
            NativeLibrary.Free(library);
            return null;
        }

        if (!NativeLibrary.TryLoad("libglib-2.0.so.0", out var glibLibrary)) {
            NativeLibrary.Free(objectLibrary);
            NativeLibrary.Free(library);
            return null;
        }

        try {
            var api = new LibNotifyNative(library, objectLibrary, glibLibrary);
            if (!api.IsInitialized) {
                api.Dispose();
                return null;
            }

            return api;
        } catch {
            NativeLibrary.Free(glibLibrary);
            NativeLibrary.Free(objectLibrary);
            NativeLibrary.Free(library);
            return null;
        }
    }

    public void Pump() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var context = DefaultMainContext();
        while (MainContextIteration(context, mayBlock: 0) != 0) {
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        if (_initialized) {
            Uninit();
            _initialized = false;
        }

        NativeLibrary.Free(_glibLibrary);
        NativeLibrary.Free(_objectLibrary);
        NativeLibrary.Free(_library);
    }

    private static T Export<T>(IntPtr library, string name)
        where T : Delegate {
        return Marshal.GetDelegateForFunctionPointer<T>(
            NativeLibrary.GetExport(library, name));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int notify_init([MarshalAs(UnmanagedType.LPUTF8Str)] string appName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_uninit();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr notify_notification_new(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string summary,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? body,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? icon);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int notify_notification_update(
        IntPtr notification,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string summary,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? body,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? icon);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int notify_notification_show(IntPtr notification, out IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_notification_set_timeout(IntPtr notification, int timeout);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_notification_add_action(
        IntPtr notification,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string action,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
        IntPtr callback,
        IntPtr userData,
        IntPtr freeFunc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_notification_clear_actions(IntPtr notification);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int notify_notification_close(IntPtr notification, out IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int notify_notification_get_closed_reason(IntPtr notification);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr notify_get_server_caps();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_notification_set_app_name(
        IntPtr notification,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string appName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_notification_set_hint_string(
        IntPtr notification,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_notification_set_hint_byte(
        IntPtr notification,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string key,
        byte value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate ulong g_signal_connect_data(
        IntPtr instance,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string signal,
        IntPtr callback,
        IntPtr userData,
        IntPtr destroyData,
        uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_signal_handler_disconnect(IntPtr instance, ulong handlerId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_object_unref(IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_main_context_default();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int g_main_context_iteration(IntPtr context, int mayBlock);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_error_free(IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_list_free(IntPtr list);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_action_callback(
        IntPtr notification,
        IntPtr action,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void notify_closed_callback(IntPtr notification, IntPtr userData);

    [StructLayout(LayoutKind.Sequential)]
    internal struct GList {
        internal IntPtr Data;
        internal IntPtr Next;
        internal IntPtr Previous;
    }
}
