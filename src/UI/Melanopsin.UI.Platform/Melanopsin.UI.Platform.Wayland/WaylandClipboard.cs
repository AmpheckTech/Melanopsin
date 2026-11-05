using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NWayland;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed class WaylandClipboard : IPlatformClipboard, IDisposable {
    private const int OpenCloseOnExec = 0x80000;
    private const int MaximumTextBytes = 16 * 1024 * 1024;
    private static readonly string[] TextMimeTypes = [
        "text/plain;charset=utf-8",
        "text/plain",
        "UTF8_STRING",
    ];

    private readonly WaylandPlatformHost _host;
    private readonly WaylandConnection _connection;
    private readonly Dictionary<IntPtr, OfferState> _offers = [];
    private WlDataDevice? _dataDevice;
    private WlDataOffer? _selection;
    private WlDataSource? _source;
    private bool _disposed;

    public WaylandClipboard(WaylandPlatformHost host) {
        _host = host;
        _connection = host.Connection;
        if (_connection.DataDeviceManager is not { } manager ||
            _connection.Seat is not { } seat) {
            return;
        }

        try {
            _dataDevice = manager.GetDataDevice(
                seat,
                new DataDeviceListener(this),
                _connection.Queue);
            _connection.FlushChecked();
        } catch {
            Dispose();
        }
    }

    public bool IsSupported => !_disposed && _dataDevice is not null;

    public ValueTask SetTextAsync(
        string text,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();
        var serial = _host.InputSerial;
        if (serial == 0) {
            throw new InvalidOperationException(
                "Wayland clipboard writes require prior user input.");
        }

        ReleaseSource();
        var listener = new DataSourceListener(text);
        var source = _connection.DataDeviceManager!.CreateDataSource(
            listener,
            _connection.Queue);
        listener.Source = source;
        foreach (var mimeType in TextMimeTypes) {
            source.Offer(mimeType);
        }

        _dataDevice!.SetSelection(source, serial);
        _source = source;
        _connection.FlushChecked();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<string?> GetTextAsync(
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();
        if (_selection is null ||
            !_offers.TryGetValue(_selection.Handle, out var state)) {
            return null;
        }

        var mimeType = TextMimeTypes.FirstOrDefault(state.MimeTypes.Contains);
        if (mimeType is null) {
            return null;
        }

        var pipe = CreatePipe();
        try {
            _selection.Receive(mimeType, pipe.WriteFd);
            _connection.FlushChecked();
        } catch {
            pipe.Dispose();
            throw;
        }

        using var writeHandle = new SafeFileHandle(
            (IntPtr)pipe.TakeWriteFd(),
            ownsHandle: true);
        writeHandle.Dispose();
        using var readHandle = new SafeFileHandle(
            (IntPtr)pipe.TakeReadFd(),
            ownsHandle: true);
        await using var stream = new FileStream(
            readHandle,
            FileAccess.Read,
            bufferSize: 4096,
            isAsync: true);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (buffer.Length <= MaximumTextBytes) {
            var read = await stream.ReadAsync(chunk, cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length > MaximumTextBytes) {
            throw new InvalidDataException(
                $"The Wayland clipboard text exceeds {MaximumTextBytes} bytes.");
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        ReleaseSource();
        DisposeSelection();
        foreach (var offer in _offers.Values) {
            offer.Proxy.Dispose();
        }

        _offers.Clear();
        if (_dataDevice is { } device) {
            if (device.IsReleaseAvailable) {
                device.Release();
            }

            device.Dispose();
            _dataDevice = null;
        }
    }

    private void AddOffer(
        WlDataOffer offer,
        string mimeType) {
        if (!_offers.TryGetValue(offer.Handle, out var state)) {
            state = new OfferState(offer);
            _offers.Add(offer.Handle, state);
        }

        state.MimeTypes.Add(mimeType);
    }

    private void Select(WlDataOffer? offer) {
        if (_selection is not null &&
            !ReferenceEquals(_selection, offer)) {
            DestroyOffer(_selection);
        }

        _selection = offer;
    }

    private void ReleaseSource() {
        if (_source is not { } source) {
            return;
        }

        _source = null;
        if (!source.IsDisposed) {
            source.Destroy();
            source.Dispose();
        }
    }

    private void DisposeSelection() {
        if (_selection is not null) {
            DestroyOffer(_selection);
            _selection = null;
        }
    }

    private void DestroyOffer(WlDataOffer offer) {
        _offers.Remove(offer.Handle);
        if (!offer.IsDisposed) {
            offer.Destroy();
            offer.Dispose();
        }
    }

    private void EnsureSupported() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_dataDevice is null || _connection.DataDeviceManager is null) {
            throw new PlatformNotSupportedException(
                "The compositor does not expose wl_data_device_manager.");
        }
    }

    private static PipePair CreatePipe() {
        Span<int> descriptors = stackalloc int[2];
        unsafe {
            fixed (int* pointer = descriptors) {
                if (Pipe2(pointer, OpenCloseOnExec) != 0) {
                    throw new IOException(
                        $"Creating a Wayland clipboard pipe failed (errno {Marshal.GetLastPInvokeError()}).");
                }
            }
        }

        return new PipePair(descriptors[0], descriptors[1]);
    }

    [DllImport("libc", EntryPoint = "pipe2", SetLastError = true)]
    private static extern unsafe int Pipe2(int* descriptors, int flags);

    private sealed class DataDeviceListener : WlDataDevice.Listener {
        private readonly WaylandClipboard _clipboard;

        public DataDeviceListener(WaylandClipboard clipboard) {
            _clipboard = clipboard;
        }

        protected override void DataOffer(
            WlDataDevice eventSender,
            NewId<WlDataOffer, WlDataOffer.Listener> id) {
            _ = id.GetAndConsume(new DataOfferListener(_clipboard));
        }

        protected override void Selection(
            WlDataDevice eventSender,
            WlDataOffer? offer) {
            _clipboard.Select(offer);
        }
    }

    private sealed class DataOfferListener : WlDataOffer.Listener {
        private readonly WaylandClipboard _clipboard;

        public DataOfferListener(WaylandClipboard clipboard) {
            _clipboard = clipboard;
        }

        protected override void Offer(
            WlDataOffer eventSender,
            string mimeType) {
            _clipboard.AddOffer(eventSender, mimeType);
        }
    }

    private sealed class DataSourceListener : WlDataSource.Listener {
        private readonly byte[] _text;

        public DataSourceListener(string text) {
            _text = System.Text.Encoding.UTF8.GetBytes(text);
        }

        public WlDataSource? Source { get; set; }

        protected override void Send(
            WlDataSource eventSender,
            string? mimeType,
            WaylandFd fd) {
            if (!TextMimeTypes.Contains(mimeType)) {
                fd.Dispose();
                return;
            }

            var rawFd = fd.Consume();
            using var handle = new SafeFileHandle(
                (IntPtr)rawFd,
                ownsHandle: true);
            using var stream = new FileStream(
                handle,
                FileAccess.Write,
                bufferSize: 4096,
                isAsync: false);
            stream.Write(_text);
        }

        protected override void Cancelled(WlDataSource eventSender) {
            if (!eventSender.IsDisposed) {
                eventSender.Destroy();
                eventSender.Dispose();
            }
        }
    }

    private sealed class OfferState(WlDataOffer proxy) {
        public WlDataOffer Proxy { get; } = proxy;

        public HashSet<string> MimeTypes { get; } = [];
    }

    private sealed class PipePair : IDisposable {
        private int _readFd;
        private int _writeFd;

        public PipePair(int readFd, int writeFd) {
            _readFd = readFd;
            _writeFd = writeFd;
        }

        public int ReadFd => _readFd;

        public int WriteFd => _writeFd;

        public int TakeReadFd() => Exchange(ref _readFd);

        public int TakeWriteFd() => Exchange(ref _writeFd);

        public void Dispose() {
            Close(TakeReadFd());
            Close(TakeWriteFd());
        }

        private static int Exchange(ref int value) {
            var result = value;
            value = -1;
            return result;
        }

        private static void Close(int fd) {
            if (fd >= 0) {
                _ = CloseNative(fd);
            }
        }

        [DllImport("libc", EntryPoint = "close")]
        private static extern int CloseNative(int fd);
    }
}
