using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaImageResourceCache : IDisposable {
    private const int MaxEntries = 64;
    private const int MaxDecodedDimension = 16384;
    private const long MaxDecodedPixels = 32L * 1024 * 1024;
    private const long MaxDecodedBytes = 128L * 1024 * 1024;
    private readonly Dictionary<ImageResource, CacheEntry> _entries =
        new(ReferenceEqualityComparer.Instance);
    private long _decodedBytes;
    private long _tick;
    private bool _disposed;

    public SKImage Get(ImageResource resource) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_entries.TryGetValue(resource, out var cached)) {
            cached.LastUsed = ++_tick;
            return cached.Image;
        }

        using var encoded = SKData.CreateCopy(resource.Data.Span);
        using var codec = SKCodec.Create(encoded) ??
            throw new InvalidDataException(
                "The image resource could not be decoded.");
        var decodedBytes = GetDecodedByteCount(codec.Info);
        var image = SKImage.FromEncodedData(encoded) ??
            throw new InvalidDataException(
                "The image resource could not be decoded.");
        var entry = new CacheEntry(image, decodedBytes, ++_tick);
        _entries.Add(resource, entry);
        _decodedBytes += entry.DecodedBytes;
        Trim();
        return image;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        Exception? failure = null;
        try {
            foreach (var entry in _entries.Values) {
                SkiaCleanup.Capture(ref failure, entry.Dispose);
            }
        } finally {
            _entries.Clear();
            _decodedBytes = 0;
            _tick = 0;
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }

    private void Trim() {
        Exception? failure = null;
        while (_entries.Count > MaxEntries ||
               _decodedBytes > MaxDecodedBytes) {
            ImageResource? victim = null;
            var oldest = long.MaxValue;
            foreach (var pair in _entries) {
                if (pair.Value.LastUsed >= oldest) {
                    continue;
                }

                oldest = pair.Value.LastUsed;
                victim = pair.Key;
            }

            if (victim is null) {
                break;
            }

            var entry = _entries[victim];
            _entries.Remove(victim);
            _decodedBytes -= entry.DecodedBytes;
            SkiaCleanup.Capture(ref failure, entry.Dispose);
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }

    private static long GetDecodedByteCount(SKImageInfo info) {
        if (info.Width <= 0 ||
            info.Height <= 0 ||
            info.Width > MaxDecodedDimension ||
            info.Height > MaxDecodedDimension ||
            (long)info.Width * info.Height > MaxDecodedPixels) {
            throw new InvalidDataException(
                "The decoded image dimensions exceed the supported limit.");
        }

        return (long)info.Width * info.Height * 4;
    }

    private sealed class CacheEntry : IDisposable {
        public CacheEntry(SKImage image, long decodedBytes, long lastUsed) {
            Image = image;
            DecodedBytes = decodedBytes;
            LastUsed = lastUsed;
        }

        public SKImage Image { get; }

        public long DecodedBytes { get; }

        public long LastUsed { get; set; }

        public void Dispose() {
            Image.Dispose();
        }
    }
}
