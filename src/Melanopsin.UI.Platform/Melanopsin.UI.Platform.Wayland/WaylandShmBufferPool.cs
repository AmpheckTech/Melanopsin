using System.Runtime.InteropServices;
using NWayland;
using NWayland.Protocols.Wayland;

namespace Melanopsin.UI.Platform.Wayland;

internal sealed unsafe partial class WaylandShmBufferPool : IDisposable {
    private const int MaximumBufferCount = 3;
    private const int ProtRead = 1;
    private const int ProtWrite = 2;
    private const int MapShared = 1;
    private const uint MfdCloseOnExec = 1;

    private readonly WlShm _shm;
    private readonly IWlTargetQueue _queue;
    private readonly Action _onBufferReleased;
    private readonly List<WaylandShmBuffer> _buffers = [];
    private bool _disposed;

    public WaylandShmBufferPool(
        WlShm shm,
        IWlTargetQueue queue,
        Action onBufferReleased) {
        _shm = shm;
        _queue = queue;
        _onBufferReleased = onBufferReleased;
    }

    public bool TryAcquire(
        int width,
        int height,
        int stride,
        out WaylandShmBuffer? buffer) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        buffer = null;
        foreach (var candidate in _buffers) {
            if (!candidate.IsInUse &&
                !candidate.IsReserved &&
                candidate.Width == width &&
                candidate.Height == height &&
                candidate.Stride == stride) {
                candidate.Reserve();
                buffer = candidate;
                return true;
            }
        }

        // Buffers are size-specific. During interactive resize the pool would
        // otherwise fill with released buffers from old sizes and permanently
        // reject every new size once MaximumBufferCount is reached. Evict idle
        // mismatched buffers before applying backpressure.
        if (_buffers.Count >= MaximumBufferCount) {
            for (var index = _buffers.Count - 1;
                 index >= 0 && _buffers.Count >= MaximumBufferCount;
                 index--) {
                var candidate = _buffers[index];
                if (candidate.IsInUse || candidate.IsReserved) {
                    continue;
                }

                _buffers.RemoveAt(index);
                candidate.Dispose();
            }
        }

        if (_buffers.Count >= MaximumBufferCount) {
            return false;
        }

        buffer = CreateBuffer(width, height, stride);
        buffer.Reserve();
        return true;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        foreach (var buffer in _buffers) {
            buffer.Dispose();
        }

        _buffers.Clear();
    }

    private WaylandShmBuffer CreateBuffer(
        int width,
        int height,
        int stride) {
        var size = checked(stride * height);
        var fd = LibcNative.MemfdCreate("melanopsin-shm", MfdCloseOnExec);
        if (fd < 0) {
            throw new InvalidOperationException(
                $"memfd_create failed with errno {Marshal.GetLastPInvokeError()}.");
        }

        var mapped = IntPtr.Zero;
        WlShmPool? pool = null;
        try {
            if (LibcNative.Ftruncate(fd, size) != 0) {
                throw new InvalidOperationException(
                    $"ftruncate failed with errno {Marshal.GetLastPInvokeError()}.");
            }

            mapped = LibcNative.Mmap(
                IntPtr.Zero,
                (nuint)size,
                ProtRead | ProtWrite,
                MapShared,
                fd,
                0);
            if (mapped == new IntPtr(-1)) {
                mapped = IntPtr.Zero;
                throw new InvalidOperationException(
                    $"mmap failed with errno {Marshal.GetLastPInvokeError()}.");
            }

            pool = _shm.CreatePool(
                fd,
                size,
                null,
                null);
            var buffer = new WaylandShmBuffer(
                this,
                pool,
                mapped,
                size,
                width,
                height,
                stride);
            _buffers.Add(buffer);
            pool = null;
            mapped = IntPtr.Zero;
            return buffer;
        } finally {
            if (mapped != IntPtr.Zero) {
                _ = LibcNative.Munmap(mapped, (nuint)size);
            }

            pool?.Dispose();
            _ = LibcNative.Close(fd);
        }
    }

    private void OnBufferReleased() {
        if (!_disposed) {
            _onBufferReleased();
        }
    }

    internal sealed unsafe class WaylandShmBuffer : IDisposable {
        private readonly WaylandShmBufferPool _owner;
        private readonly ReleaseListener _releaseListener;
        private WlShmPool? _pool;
        private IntPtr _data;
        private readonly int _size;
        private bool _disposed;

        internal WaylandShmBuffer(
            WaylandShmBufferPool owner,
            WlShmPool pool,
            IntPtr data,
            int size,
            int width,
            int height,
            int stride) {
            _owner = owner;
            _pool = pool;
            _data = data;
            _size = size;
            Width = width;
            Height = height;
            Stride = stride;
            _releaseListener = new ReleaseListener(this);
            Buffer = pool.CreateBuffer(
                0,
                width,
                height,
                stride,
                WlShm.FormatEnum.Xrgb8888,
                _releaseListener,
                owner._queue);
        }

        public WlBuffer Buffer { get; }

        public int Width { get; }

        public int Height { get; }

        public int Stride { get; }

        public bool IsInUse { get; private set; }

        public bool IsReserved { get; private set; }

        internal void Reserve() {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsReserved || IsInUse) {
                throw new InvalidOperationException(
                    "The Wayland shared-memory buffer is already in use.");
            }

            IsReserved = true;
        }

        internal void ReleaseReservation() {
            if (!IsReserved) {
                return;
            }

            IsReserved = false;
        }

        internal void Commit(ReadOnlySpan<byte> pixels) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsReserved || IsInUse) {
                throw new InvalidOperationException(
                    "The Wayland shared-memory buffer was not reserved.");
            }

            var byteCount = checked(Stride * Height);
            if (pixels.Length < byteCount || byteCount > _size) {
                throw new ArgumentException(
                    "The pixel buffer is smaller than the Wayland buffer.",
                    nameof(pixels));
            }

            pixels[..byteCount].CopyTo(new Span<byte>((void*)_data, byteCount));
            IsReserved = false;
            IsInUse = true;
        }

        internal void AbortCommit() {
            if (_disposed || (!IsReserved && !IsInUse)) {
                return;
            }

            IsReserved = false;
            IsInUse = false;
            _owner.OnBufferReleased();
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _disposed = true;
            if (!Buffer.IsDisposed) {
                Buffer.Destroy();
                Buffer.Dispose();
            }

            if (_pool is not null) {
                if (!_pool.IsDisposed) {
                    _pool.Destroy();
                    _pool.Dispose();
                }

                _pool = null;
            }

            if (_data != IntPtr.Zero) {
                _ = LibcNative.Munmap(_data, (nuint)_size);
                _data = IntPtr.Zero;
            }
        }

        private void MarkReleased() {
            IsInUse = false;
            _owner.OnBufferReleased();
        }

        private sealed class ReleaseListener : WlBuffer.Listener {
            private readonly WaylandShmBuffer _buffer;

            public ReleaseListener(WaylandShmBuffer buffer) {
                _buffer = buffer;
            }

            protected override void Release(WlBuffer eventSender) {
                _buffer.MarkReleased();
            }
        }
    }

    internal static partial class LibcNative {
        [LibraryImport("libc", EntryPoint = "memfd_create", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int MemfdCreate(string name, uint flags);

        [LibraryImport("libc", EntryPoint = "ftruncate")]
        internal static partial int Ftruncate(int fd, long length);

        [LibraryImport("libc", EntryPoint = "mmap")]
        internal static partial IntPtr Mmap(
            IntPtr address,
            nuint length,
            int protection,
            int flags,
            int fd,
            long offset);

        [LibraryImport("libc", EntryPoint = "munmap")]
        internal static partial int Munmap(IntPtr address, nuint length);

        [LibraryImport("libc", EntryPoint = "close")]
        internal static partial int Close(int fd);
    }
}
