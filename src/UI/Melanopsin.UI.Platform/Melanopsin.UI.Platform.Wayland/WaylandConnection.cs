using System.Runtime.InteropServices;
using NWayland;
using NWayland.Protocols.CursorShapeV1;
using NWayland.Protocols.FractionalScaleV1;
using NWayland.Protocols.TextInputUnstableV3;
using NWayland.Protocols.Viewporter;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandConnection : IDisposable {
    private readonly WlDisplay _display;
    private readonly RegistryListener _registryListener;
    private readonly SeatListener _seatListener;
    private readonly OutputListener _outputListener;
    private readonly List<WlOutput> _outputs = [];
    private bool _disposed;

    public WaylandConnection(string? displayName) {
        _display = WlDisplay.Connect(displayName);
        _registryListener = new RegistryListener(this);
        _seatListener = new SeatListener(this);
        _outputListener = new OutputListener(this);

        try {
            // Keep every managed proxy on libwayland's default queue. libdecor's
            // plugin dispatch path drives that queue directly, so splitting
            // protocol events onto a private queue would leave xdg-shell and
            // frame-callback events stranded until a later dispatch.
            Registry = _display.GetRegistry(_registryListener, _display);
            if (_display.Roundtrip() < 0) {
                throw new InvalidOperationException(
                    "Wayland failed while discovering global objects.");
            }

            if (Compositor is null) {
                throw new InvalidOperationException(
                    "The Wayland compositor does not expose wl_compositor.");
            }

            if (Shm is null) {
                throw new InvalidOperationException(
                    "The Wayland compositor does not expose wl_shm.");
            }
        } catch {
            Dispose();
            throw;
        }
    }

    public WlDisplay Display => _display;

    public IWlTargetQueue Queue => _display;

    public WlRegistry Registry { get; }

    public WlCompositor? Compositor { get; private set; }

    public WlShm? Shm { get; private set; }

    public WlSeat? Seat { get; private set; }

    public WlDataDeviceManager? DataDeviceManager { get; private set; }

    public WpFractionalScaleManagerV1? FractionalScaleManager { get; private set; }

    public WpCursorShapeManagerV1? CursorShapeManager { get; private set; }

    public ZwpTextInputManagerV3? TextInputManager { get; private set; }

    public WpViewporter? Viewporter { get; private set; }

    public WlSeat.CapabilityEnum SeatCapabilities { get; private set; }

    public event Action<WlSeat.CapabilityEnum>? SeatCapabilitiesChanged;

    public int DispatchPending() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _display.DispatchPending();
    }

    public int Flush() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _display.Flush();
    }

    public void FlushChecked() {
        var result = Flush();
        if (result >= 0) {
            return;
        }

        // wl_display_flush never blocks. EAGAIN means the socket is currently
        // full; the request remains buffered and will be retried later.
        if (Marshal.GetLastPInvokeError() == 11) {
            return;
        }

        throw new InvalidOperationException(
            $"Wayland failed to flush the display connection (errno {Marshal.GetLastPInvokeError()}).");
    }

    public void NotifySeatCapabilities(WlSeat.CapabilityEnum capabilities) {
        SeatCapabilities = capabilities;
        SeatCapabilitiesChanged?.Invoke(capabilities);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        DisposeProxy(TextInputManager, static value => value.Destroy());
        DisposeProxy(CursorShapeManager, static value => value.Destroy());
        DisposeProxy(Viewporter, static value => value.Destroy());
        DisposeProxy(FractionalScaleManager, static value => value.Destroy());
        DisposeProxy(
            DataDeviceManager,
            static value => {
                if (value.IsReleaseAvailable) {
                    value.Release();
                }
            });
        foreach (var output in _outputs) {
            DisposeProxy(
                output,
                static value => {
                    if (value.IsReleaseAvailable) {
                        value.Release();
                    }
                });
        }

        _outputs.Clear();
        DisposeProxy(
            Seat,
            static value => {
                if (value.IsReleaseAvailable) {
                    value.Release();
                }
            });
        DisposeProxy(
            Shm,
            static value => {
                if (value.IsReleaseAvailable) {
                    value.Release();
                }
            });
        DisposeProxy(
            Compositor,
            static value => {
                if (value.IsReleaseAvailable) {
                    value.Release();
                }
            });
        DisposeProxy(Registry, static _ => { });
        _display.Dispose();
    }

    private void RegisterGlobal(
        uint name,
        string interfaceName,
        uint version) {
        switch (interfaceName) {
            case "wl_compositor":
                Compositor = Registry.Bind<WlCompositor>(
                    name,
                    Math.Min(version, 6),
                    listener: null,
                    queue: null);
                break;
            case "wl_shm":
                Shm = Registry.Bind<WlShm>(
                    name,
                    1,
                    listener: null,
                    queue: null);
                break;
            case "wl_seat":
                Seat = Registry.Bind<WlSeat>(
                    name,
                    Math.Min(version, 8),
                    _seatListener,
                    _display);
                break;
            case "wl_data_device_manager":
                DataDeviceManager = Registry.Bind<WlDataDeviceManager>(
                    name,
                    Math.Min(version, 3),
                    listener: null,
                    queue: null);
                break;
            case "wl_output":
                _outputs.Add(Registry.Bind<WlOutput>(
                    name,
                    Math.Min(version, 4),
                    _outputListener,
                    _display));
                break;
            case "wp_fractional_scale_manager_v1":
                FractionalScaleManager =
                    Registry.Bind<WpFractionalScaleManagerV1>(
                        name,
                        1,
                        listener: null,
                        queue: null);
                break;
            case "wp_cursor_shape_manager_v1":
                CursorShapeManager =
                    Registry.Bind<WpCursorShapeManagerV1>(
                        name,
                        1,
                        listener: null,
                        queue: null);
                break;
            case "zwp_text_input_manager_v3":
                TextInputManager = Registry.Bind<ZwpTextInputManagerV3>(
                    name,
                    Math.Min(version, 2),
                    listener: null,
                    queue: null);
                break;
            case "wp_viewporter":
                Viewporter = Registry.Bind<WpViewporter>(
                    name,
                    1,
                    listener: null,
                    queue: null);
                break;
        }
    }

    private static void DisposeProxy<T>(
        T? proxy,
        Action<T> destroy)
        where T : WlProxy {
        if (proxy is null || proxy.IsDisposed) {
            return;
        }

        try {
            destroy(proxy);
        } finally {
            proxy.Dispose();
        }
    }

    private sealed class RegistryListener : WlRegistry.Listener {
        private readonly WaylandConnection _connection;

        public RegistryListener(WaylandConnection connection) {
            _connection = connection;
        }

        protected override void Global(
            WlRegistry sender,
            uint name,
            string @interface,
            uint version) {
            _connection.RegisterGlobal(name, @interface, version);
        }

        protected override void GlobalRemove(WlRegistry sender, uint name) {
        }
    }

    private sealed class SeatListener : WlSeat.Listener {
        private readonly WaylandConnection _connection;

        public SeatListener(WaylandConnection connection) {
            _connection = connection;
        }

        protected override void Capabilities(
            WlSeat eventSender,
            WlSeat.CapabilityEnum capabilities) {
            _connection.NotifySeatCapabilities(capabilities);
        }

        protected override void Name(WlSeat eventSender, string name) {
        }
    }

    private sealed class OutputListener : WlOutput.Listener {
        private readonly WaylandConnection _connection;

        public OutputListener(WaylandConnection connection) {
            _connection = connection;
        }

        protected override void Geometry(
            WlOutput eventSender,
            int x,
            int y,
            int physicalWidth,
            int physicalHeight,
            WlOutput.SubpixelEnum subpixel,
            string make,
            string model,
            WlOutput.TransformEnum transform) {
        }

        protected override void Mode(
            WlOutput eventSender,
            WlOutput.ModeEnum flags,
            int width,
            int height,
            int refresh) {
        }

        protected override void Done(WlOutput eventSender) {
        }

        protected override void Scale(WlOutput eventSender, int factor) {
            // Integer output scale is not a window scale. The compositor
            // reports the scale that applies to a surface through
            // wl_surface.preferred_buffer_scale, or more precisely through
            // wp_fractional_scale_v1 when that protocol is available. Keeping a
            // global max here makes a 1x window jump to 2x whenever any output
            // advertises an integer scale.
        }

        protected override void Name(WlOutput eventSender, string name) {
        }

        protected override void Description(
            WlOutput eventSender,
            string description) {
        }
    }
}
