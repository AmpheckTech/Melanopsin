using SkiaSharp;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaImageResourceCache : IDisposable {
    private const int MaxEntries = 64;
    private const long MaxEncodedBytes = 64L * 1024 * 1024;
    private readonly Dictionary<ImageResource, CacheEntry> _entries =
        new(ReferenceEqualityComparer.Instance);
    private long _encodedBytes;
    private long _tick;
    private bool _disposed;

    public SKImage Get(ImageResource resource) {
        ArgumentNullException.ThrowIfNull(resource);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_entries.TryGetValue(resource, out var cached)) {
            cached.LastUsed = ++_tick;
            return cached.Image;
        }

        var image = SKImage.FromEncodedData(resource.Data.Span) ??
            throw new InvalidDataException(
                "The image resource could not be decoded.");
        var entry = new CacheEntry(image, resource.Data.Length, ++_tick);
        _entries.Add(resource, entry);
        _encodedBytes += entry.EncodedBytes;
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
            _encodedBytes = 0;
            _tick = 0;
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }

    private void Trim() {
        Exception? failure = null;
        while (_entries.Count > MaxEntries ||
               _encodedBytes > MaxEncodedBytes) {
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
            _encodedBytes -= entry.EncodedBytes;
            SkiaCleanup.Capture(ref failure, entry.Dispose);
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }

    private sealed class CacheEntry : IDisposable {
        public CacheEntry(SKImage image, long encodedBytes, long lastUsed) {
            Image = image;
            EncodedBytes = encodedBytes;
            LastUsed = lastUsed;
        }

        public SKImage Image { get; }

        public long EncodedBytes { get; }

        public long LastUsed { get; set; }

        public void Dispose() {
            Image.Dispose();
        }
    }
}
