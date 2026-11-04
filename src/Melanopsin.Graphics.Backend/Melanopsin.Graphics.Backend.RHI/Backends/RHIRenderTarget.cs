using Melanopsin.Graphics.Rendering;
using Silk.NET.Maths;

namespace Melanopsin.Graphics.Backend.RHI;

internal sealed class RHIRenderTarget :
    IResizableRenderTarget,
    IRenderFrameLeaseOwner {
    private const long LeaseHeldBit = 1;
    private const long MaxLeaseGeneration = long.MaxValue >> 1;

    private readonly RHIFrameSurface _surface;
    private byte[] _pixels;
    private long _leaseState;
    private DamageRegion _damage;
    private RenderStatistics _statistics;
    private AccessibilityVisualOptions _visualAccessibility;
    private bool _disposed;

    public RHIRenderTarget(RenderTargetDescriptor descriptor) {
        Descriptor = descriptor;
        _pixels = CreatePixelBuffer(descriptor.Size);
        _surface = new RHIFrameSurface(this);
        _damage = DamageRegion.None(descriptor.Size);
    }

    public RenderTargetDescriptor Descriptor { get; private set; }

    public bool IsFrameAvailable =>
        !_disposed && (Volatile.Read(ref _leaseState) & LeaseHeldBit) == 0;

    public bool TryRender(RenderScene scene, out RenderFrameLease frame) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (scene.Size != Descriptor.Size) {
            throw new ArgumentException(
                "The render scene size does not match the target.",
                nameof(scene));
        }

        if (!TryAcquireLease(out var generation)) {
            frame = default;
            return false;
        }

        try {
            FillGradient();
            _damage = scene.Damage;
            _statistics = scene.Statistics;
            _visualAccessibility = scene.VisualAccessibility;
            frame = new RenderFrameLease(this, generation);
            return true;
        } catch {
            ((IRenderFrameLeaseOwner)this).ReleaseLease(generation);
            frame = default;
            throw;
        }
    }

    public bool TryResize(RenderTargetDescriptor descriptor) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsFrameAvailable) {
            return false;
        }

        if (descriptor.Size == Descriptor.Size) {
            return true;
        }

        _pixels = CreatePixelBuffer(descriptor.Size);
        Descriptor = descriptor;
        _damage = DamageRegion.None(descriptor.Size);
        _statistics = default;
        _visualAccessibility = default;
        return true;
    }

    public void Dispose() {
        _disposed = true;
        Interlocked.Exchange(ref _leaseState, 0);
    }

    private static byte[] CreatePixelBuffer(PixelSize size) {
        return new byte[checked(size.Width * size.Height * 4)];
    }

    private void FillGradient() {
        var width = Descriptor.Size.Width;
        var height = Descriptor.Size.Height;
        var stride = checked(width * 4);

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var index = checked((y * stride) + (x * 4));
                var color = 255 * Vector3D.Normalize(new Vector3D<float> {
                    X = x,
                    Y = y,
                    Z = 1145,
                });

                _pixels[index] = (byte)color.X;
                _pixels[index + 1] = (byte)color.Y;
                _pixels[index + 2] = (byte)color.Z;
                _pixels[index + 3] = byte.MaxValue;
            }
        }
    }

    private bool TryAcquireLease(out long generation) {
        while (true) {
            var state = Volatile.Read(ref _leaseState);
            if ((state & LeaseHeldBit) != 0) {
                generation = 0;
                return false;
            }

            generation = (state >> 1) + 1;
            if (generation > MaxLeaseGeneration) {
                throw new InvalidOperationException(
                    "The render target exhausted its frame lease generations.");
            }

            if (Interlocked.CompareExchange(
                    ref _leaseState,
                    EncodeLeaseState(generation, held: true),
                    state) == state) {
                return true;
            }
        }
    }

    private static long EncodeLeaseState(long generation, bool held) {
        return (generation << 1) | (held ? LeaseHeldBit : 0);
    }

    private static void ValidateLease(bool disposed, long state, long generation) {
        if (disposed || state != EncodeLeaseState(generation, held: true)) {
            throw new InvalidOperationException(
                "The render frame lease is stale or has already been released.");
        }
    }

    bool IRenderFrameLeaseOwner.IsLeaseValid(long generation) {
        return !_disposed &&
            Volatile.Read(ref _leaseState) ==
                EncodeLeaseState(generation, held: true);
    }

    long IRenderFrameLeaseOwner.GetSequence(long generation) {
        ValidateLease(
            _disposed,
            Volatile.Read(ref _leaseState),
            generation);
        return generation;
    }

    IRenderSurface IRenderFrameLeaseOwner.GetSurface(long generation) {
        ValidateLease(
            _disposed,
            Volatile.Read(ref _leaseState),
            generation);
        return _surface;
    }

    DamageRegion IRenderFrameLeaseOwner.GetDamage(long generation) {
        ValidateLease(
            _disposed,
            Volatile.Read(ref _leaseState),
            generation);
        return _damage;
    }

    RenderStatistics IRenderFrameLeaseOwner.GetStatistics(long generation) {
        ValidateLease(
            _disposed,
            Volatile.Read(ref _leaseState),
            generation);
        return _statistics;
    }

    AccessibilityVisualOptions IRenderFrameLeaseOwner.GetVisualAccessibility(
        long generation) {
        ValidateLease(
            _disposed,
            Volatile.Read(ref _leaseState),
            generation);
        return _visualAccessibility;
    }

    void IRenderFrameLeaseOwner.ReleaseLease(long generation) {
        _ = Interlocked.CompareExchange(
            ref _leaseState,
            EncodeLeaseState(generation, held: false),
            EncodeLeaseState(generation, held: true));
    }

    private sealed class RHIFrameSurface(RHIRenderTarget owner) :
        ICpuReadableSurface {
        public PixelSize Size => owner.Descriptor.Size;

        public RenderPixelFormat PixelFormat =>
            RenderPixelFormat.Bgra8888Premultiplied;

        public int Stride => checked(Size.Width * 4);

        public bool TryCopyPixels(
            DamageRegion damage,
            AccessibilityVisualOptions visualAccessibility,
            Span<byte> destination,
            out int stride) {
            stride = Stride;
            if (owner._disposed ||
                damage.Size != Size ||
                destination.Length < owner._pixels.Length) {
                return false;
            }

            owner._pixels.CopyTo(destination);
            return true;
        }
    }
}
