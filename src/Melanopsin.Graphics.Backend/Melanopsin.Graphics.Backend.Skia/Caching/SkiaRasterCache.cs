using System.Diagnostics.CodeAnalysis;

namespace Melanopsin.Graphics.Backend.Skia;

internal sealed class SkiaRasterCache : IDisposable {
    private readonly Dictionary<RenderLayer, RasterCacheEntry> _entries =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RenderLayer, long> _candidates =
        new(ReferenceEqualityComparer.Instance);
    private readonly SkiaRasterCacheOptions _options;
    private long _tick;
    private long _bytes;
    private bool _disposed;

    public SkiaRasterCache(SkiaRasterCacheOptions options) {
        _options = options;
    }

    public int Hits { get; private set; }

    public int Misses { get; private set; }

    public long Bytes => _bytes;

    public void BeginFrame() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _tick++;
        Hits = 0;
        Misses = 0;
    }

    public bool TryGetOrCreate(
        RetainedLayer layer,
        Func<RetainedLayer, RasterCacheEntry?> createEntry,
        [NotNullWhen(true)] out RasterCacheEntry? entry) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_entries.TryGetValue(layer, out entry)) {
            Hits++;
            entry.LastUsed = _tick;
            return true;
        }

        if (!_candidates.TryGetValue(layer, out var firstSeen) ||
            firstSeen >= _tick) {
            _candidates[layer] = _tick;
            return false;
        }

        entry = createEntry(layer);
        if (entry is null) {
            return false;
        }

        _candidates.Remove(layer);
        _entries[layer] = entry;
        _bytes += entry.Bytes;
        entry.LastUsed = _tick;
        Misses++;
        return true;
    }

    public void Trim() {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RemoveStaleCandidates();
        Exception? failure = null;
        while (_bytes > _options.ByteLimit ||
               _entries.Count > _options.EntryLimit) {
            if (!TryEvictOldest(ref failure)) {
                break;
            }
        }

        SkiaCleanup.ThrowIfFailed(failure);
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
            _candidates.Clear();
            _bytes = 0;
            Hits = 0;
            Misses = 0;
        }

        SkiaCleanup.ThrowIfFailed(failure);
    }

    private void RemoveStaleCandidates() {
        List<RenderLayer>? stale = null;
        foreach (var pair in _candidates) {
            if (pair.Value >= _tick - 1) {
                continue;
            }

            stale ??= [];
            stale.Add(pair.Key);
        }

        if (stale is null) {
            return;
        }

        foreach (var layer in stale) {
            _candidates.Remove(layer);
        }
    }

    private bool TryEvictOldest(ref Exception? failure) {
        RenderLayer? victim = null;
        var oldest = long.MaxValue;
        foreach (var pair in _entries) {
            if (pair.Value.LastUsed == _tick ||
                pair.Value.LastUsed >= oldest) {
                continue;
            }

            oldest = pair.Value.LastUsed;
            victim = pair.Key;
        }

        if (victim is null ||
            !_entries.Remove(victim, out var entry)) {
            return false;
        }

        _bytes -= entry.Bytes;
        SkiaCleanup.Capture(ref failure, entry.Dispose);
        return true;
    }
}
