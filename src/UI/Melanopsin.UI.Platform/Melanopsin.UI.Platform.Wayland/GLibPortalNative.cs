using System.Runtime.InteropServices;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class GLibPortalNative : IDisposable {
    private const uint SessionBus = 2;
    private readonly IntPtr _gio;
    private readonly IntPtr _glib;
    private readonly IntPtr _gobject;
    private bool _disposed;

    private GLibPortalNative(
        IntPtr gio,
        IntPtr glib,
        IntPtr gobject) {
        _gio = gio;
        _glib = glib;
        _gobject = gobject;
        BusGetSync = Export<g_bus_get_sync>(gio, "g_bus_get_sync");
        ConnectionCallSync = Export<g_dbus_connection_call_sync>(
            gio,
            "g_dbus_connection_call_sync");
        SignalSubscribe = Export<g_dbus_connection_signal_subscribe>(
            gio,
            "g_dbus_connection_signal_subscribe");
        SignalUnsubscribe = Export<g_dbus_connection_signal_unsubscribe>(
            gio,
            "g_dbus_connection_signal_unsubscribe");
        MainContextDefault = Export<g_main_context_default>(
            glib,
            "g_main_context_default");
        MainContextIteration = Export<g_main_context_iteration>(
            glib,
            "g_main_context_iteration");
        VariantTypeNew = Export<g_variant_type_new>(glib, "g_variant_type_new");
        VariantTypeFree = Export<g_variant_type_free>(glib, "g_variant_type_free");
        VariantBuilderNew = Export<g_variant_builder_new>(
            glib,
            "g_variant_builder_new");
        VariantBuilderAddValue = Export<g_variant_builder_add_value>(
            glib,
            "g_variant_builder_add_value");
        VariantBuilderEnd = Export<g_variant_builder_end>(
            glib,
            "g_variant_builder_end");
        VariantBuilderUnref = Export<g_variant_builder_unref>(
            glib,
            "g_variant_builder_unref");
        VariantNewString = Export<g_variant_new_string>(
            glib,
            "g_variant_new_string");
        VariantNewDictEntry = Export<g_variant_new_dict_entry>(
            glib,
            "g_variant_new_dict_entry");
        VariantNewVariant = Export<g_variant_new_variant>(
            glib,
            "g_variant_new_variant");
        VariantRefSink = Export<g_variant_ref_sink>(glib, "g_variant_ref_sink");
        VariantUnref = Export<g_variant_unref>(glib, "g_variant_unref");
        VariantGetChildValue = Export<g_variant_get_child_value>(
            glib,
            "g_variant_get_child_value");
        VariantGetVariant = Export<g_variant_get_variant>(
            glib,
            "g_variant_get_variant");
        VariantGetString = Export<g_variant_get_string>(
            glib,
            "g_variant_get_string");
        VariantGetUInt32 = Export<g_variant_get_uint32>(
            glib,
            "g_variant_get_uint32");
        VariantNChildren = Export<g_variant_n_children>(
            glib,
            "g_variant_n_children");
        CancellableNew = Export<g_cancellable_new>(gio, "g_cancellable_new");
        CancellableCancel = Export<g_cancellable_cancel>(
            gio,
            "g_cancellable_cancel");
        ObjectUnref = Export<g_object_unref>(gobject, "g_object_unref");
        ErrorFree = Export<g_error_free>(glib, "g_error_free");
    }

    public IntPtr Connection { get; set; }
    public g_bus_get_sync BusGetSync { get; }
    public g_dbus_connection_call_sync ConnectionCallSync { get; }
    public g_dbus_connection_signal_subscribe SignalSubscribe { get; }
    public g_dbus_connection_signal_unsubscribe SignalUnsubscribe { get; }
    public g_main_context_default MainContextDefault { get; }
    public g_main_context_iteration MainContextIteration { get; }
    public g_variant_type_new VariantTypeNew { get; }
    public g_variant_type_free VariantTypeFree { get; }
    public g_variant_builder_new VariantBuilderNew { get; }
    public g_variant_builder_add_value VariantBuilderAddValue { get; }
    public g_variant_builder_end VariantBuilderEnd { get; }
    public g_variant_builder_unref VariantBuilderUnref { get; }
    public g_variant_new_string VariantNewString { get; }
    public g_variant_new_dict_entry VariantNewDictEntry { get; }
    public g_variant_new_variant VariantNewVariant { get; }
    public g_variant_ref_sink VariantRefSink { get; }
    public g_variant_unref VariantUnref { get; }
    public g_variant_get_child_value VariantGetChildValue { get; }
    public g_variant_get_variant VariantGetVariant { get; }
    public g_variant_get_string VariantGetString { get; }
    public g_variant_get_uint32 VariantGetUInt32 { get; }
    public g_variant_n_children VariantNChildren { get; }
    public g_cancellable_new CancellableNew { get; }
    public g_cancellable_cancel CancellableCancel { get; }
    public g_object_unref ObjectUnref { get; }
    public g_error_free ErrorFree { get; }

    public static GLibPortalNative? TryCreate() {
        if (!NativeLibrary.TryLoad("libgio-2.0.so.0", out var gio)) {
            return null;
        }

        if (!NativeLibrary.TryLoad("libglib-2.0.so.0", out var glib)) {
            NativeLibrary.Free(gio);
            return null;
        }

        if (!NativeLibrary.TryLoad("libgobject-2.0.so.0", out var gobject)) {
            NativeLibrary.Free(glib);
            NativeLibrary.Free(gio);
            return null;
        }

        try {
            var api = new GLibPortalNative(gio, glib, gobject);
            api.Connection = api.BusGetSync(SessionBus, IntPtr.Zero, out var error);
            if (error != IntPtr.Zero) {
                api.ErrorFree(error);
            }

            if (api.Connection == IntPtr.Zero) {
                api.Dispose();
                return null;
            }

            return api;
        } catch {
            NativeLibrary.Free(gobject);
            NativeLibrary.Free(glib);
            NativeLibrary.Free(gio);
            return null;
        }
    }

    public void Pump() {
        if (_disposed) {
            return;
        }

        var context = MainContextDefault();
        while (MainContextIteration(context, mayBlock: 0) != 0) {
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        if (Connection != IntPtr.Zero) {
            ObjectUnref(Connection);
            Connection = IntPtr.Zero;
        }

        NativeLibrary.Free(_gobject);
        NativeLibrary.Free(_glib);
        NativeLibrary.Free(_gio);
    }

    private static T Export<T>(IntPtr library, string name)
        where T : Delegate {
        return Marshal.GetDelegateForFunctionPointer<T>(
            NativeLibrary.GetExport(library, name));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_bus_get_sync(
        uint busType,
        IntPtr cancellable,
        out IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_dbus_connection_call_sync(
        IntPtr connection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string busName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string objectPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string interfaceName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string methodName,
        IntPtr parameters,
        IntPtr replyType,
        uint flags,
        int timeoutMilliseconds,
        IntPtr cancellable,
        out IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint g_dbus_connection_signal_subscribe(
        IntPtr connection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? sender,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? interfaceName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? member,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? objectPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? arg0,
        uint flags,
        IntPtr callback,
        IntPtr userData,
        IntPtr destroyNotify);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_dbus_connection_signal_unsubscribe(
        IntPtr connection,
        uint subscriptionId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_main_context_default();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int g_main_context_iteration(IntPtr context, int mayBlock);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_type_new(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_variant_type_free(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_builder_new(IntPtr type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_variant_builder_add_value(IntPtr builder, IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_builder_end(IntPtr builder);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_variant_builder_unref(IntPtr builder);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_new_string(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_new_dict_entry(IntPtr key, IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_new_variant(IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_ref_sink(IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_variant_unref(IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_get_child_value(IntPtr value, nuint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_get_variant(IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_variant_get_string(IntPtr value, out nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint g_variant_get_uint32(IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nuint g_variant_n_children(IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr g_cancellable_new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_cancellable_cancel(IntPtr cancellable);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_object_unref(IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void g_error_free(IntPtr error);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void SignalCallback(
        IntPtr connection,
        IntPtr senderName,
        IntPtr objectPath,
        IntPtr interfaceName,
        IntPtr signalName,
        IntPtr parameters,
        IntPtr userData);
}
