namespace Melanopsin.UI.Controls;

/// <summary>
/// Tracks row heights and prefix offsets for a virtual list. The owner decides
/// when rows are invalidated; queries never mutate the cache.
/// </summary>
internal sealed class VirtualListRowLayout {
    private float[] _heights = [];
    private float[] _tree = [];
    private readonly HashSet<int> _dirtyRows = [];
    private Func<int, float>? _rowHeightProvider;
    private float _defaultRowHeight;
    private float _totalHeight;
    private int _itemCount;
    private bool _isInitialized;
    private bool _requiresRebuild = true;

    public float TotalHeight => _totalHeight;

    public void Invalidate() {
        _requiresRebuild = true;
        _dirtyRows.Clear();
    }

    public void InvalidateRow(int index) {
        if (index < 0 || index >= _itemCount) {
            return;
        }

        if (!_requiresRebuild) {
            _dirtyRows.Add(index);
        }
    }

    public void Ensure(
        int itemCount,
        float defaultRowHeight,
        Func<int, float>? rowHeightProvider) {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        var sourceChanged =
            !ReferenceEquals(_rowHeightProvider, rowHeightProvider) ||
            _defaultRowHeight != defaultRowHeight;
        _rowHeightProvider = rowHeightProvider;
        _defaultRowHeight = defaultRowHeight;

        if (!_isInitialized || _requiresRebuild || sourceChanged) {
            Rebuild(itemCount);
            return;
        }

        if (_itemCount != itemCount) {
            Resize(itemCount);
        }

        ApplyDirtyRows();
    }

    public float GetRowTop(int index) {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _itemCount);
        return PrefixSum(index);
    }

    public float GetRowHeight(int index) {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _itemCount);
        return _heights[index];
    }

    public int FindRowAt(float offset, int itemCount) {
        if (itemCount <= 0) {
            return 0;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(itemCount, _itemCount);
        if (!float.IsFinite(offset) || offset <= 0 || _totalHeight <= 0) {
            return 0;
        }

        if (offset >= _totalHeight) {
            return itemCount - 1;
        }

        var index = 0;
        var remaining = offset;
        for (var step = HighestPowerOfTwo(itemCount); step > 0; step >>= 1) {
            var next = index + step;
            if (next > itemCount || _tree[next] > remaining) {
                continue;
            }

            index = next;
            remaining -= _tree[next];
        }

        return Math.Min(index, itemCount - 1);
    }

    public float MaximumScrollOffset(float viewportHeight) {
        return Math.Max(0, _totalHeight - Math.Max(0, viewportHeight));
    }

    private void Rebuild(int itemCount) {
        _itemCount = itemCount;
        _heights = new float[itemCount];
        _tree = new float[itemCount + 1];
        _totalHeight = 0;
        _dirtyRows.Clear();

        for (var index = 0; index < itemCount; index++) {
            var height = ResolveHeight(index);
            _heights[index] = height;
            Add(index + 1, height);
        }

        _isInitialized = true;
        _requiresRebuild = false;
    }

    private void Resize(int itemCount) {
        if (_itemCount > itemCount) {
            var removedHeight = _totalHeight - PrefixSum(itemCount);
            _totalHeight = Math.Max(0, _totalHeight - removedHeight);
            _itemCount = itemCount;
            Array.Resize(ref _heights, itemCount);
            Array.Resize(ref _tree, itemCount + 1);
        }

        if (_itemCount < itemCount) {
            Array.Resize(ref _heights, itemCount);
            Array.Resize(ref _tree, itemCount + 1);
            while (_itemCount < itemCount) {
                var index = _itemCount;
                var height = ResolveHeight(index);
                _heights[index] = height;
                Add(index + 1, height);
                _itemCount++;
            }
        }

        _dirtyRows.RemoveWhere(index => index >= _itemCount);
    }

    private void ApplyDirtyRows() {
        if (_dirtyRows.Count == 0) {
            return;
        }

        foreach (var index in _dirtyRows) {
            if (index < 0 || index >= _itemCount) {
                continue;
            }

            var previous = _heights[index];
            var next = ResolveHeight(index);
            if (previous == next) {
                continue;
            }

            _heights[index] = next;
            Add(index + 1, next - previous);
        }

        _dirtyRows.Clear();
    }

    private float ResolveHeight(int index) {
        var height = _rowHeightProvider?.Invoke(index) ?? _defaultRowHeight;
        return float.IsFinite(height) ? Math.Max(1, height) : 1;
    }

    private void Add(int index, float delta) {
        _totalHeight += delta;
        for (var current = index; current < _tree.Length; current += current & -current) {
            _tree[current] += delta;
        }
    }

    private float PrefixSum(int count) {
        var sum = 0f;
        for (var current = count; current > 0; current -= current & -current) {
            sum += _tree[current];
        }

        return sum;
    }

    private static int HighestPowerOfTwo(int value) {
        var result = 1;
        while (result <= value / 2) {
            result <<= 1;
        }

        return result;
    }
}
