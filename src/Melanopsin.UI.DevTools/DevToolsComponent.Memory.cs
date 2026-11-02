using System.Diagnostics;
using Melanopsin.Mathematics;
using Melanopsin.Reactive;
using Melanopsin.UI.Rendering;

namespace Melanopsin.UI.DevTools;

public sealed partial class DevToolsComponent {
    private const int HeapHistoryLength = 96;
    private const string HeapChartId = "devtools-memory-chart";
    private static readonly string[] ByteUnits =
        ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
    private static readonly long MemorySampleIntervalTicks =
        Stopwatch.Frequency / 10;
    private readonly Signal<MemoryStatistics?> _memory = new(null);
    private readonly double[] _heapHistory = new double[HeapHistoryLength];
    private readonly Action<RenderCommandContext, List<RenderCommand>> _heapChartDraw;
    private Canvas? _heapChart;
    private MemoryProbe? _memoryProbe;
    private int _heapHistoryCount;
    private int _heapHistoryHead;
    private long _peakHeapBytes;
    private long _lastMemorySampleTimestamp;
    private long _lastAllocatedBytes;
    private DateTime? _memorySampleAt;
    private double _allocationBytesPerSecond;

    public DevToolsComponent() {
        _heapChartDraw = DrawHeapChart;
    }

    public string MemoryHeapValue => _memory.Value is { } memory
        ? FormatBytes(memory.ManagedHeapBytes)
        : "—";

    public string MemoryPeakValue => _memory.Value is null
        ? "—"
        : FormatBytes(_peakHeapBytes);

    public string MemoryCommittedValue => _memory.Value is { } memory
        ? FormatBytes(memory.CommittedBytes)
        : "—";

    public string MemoryAvailableValue => _memory.Value is { } memory
        ? FormatBytes(memory.AvailableBytes)
        : "—";

    public string MemoryFragmentedValue => _memory.Value is { } memory
        ? $"{FormatBytes(memory.FragmentedBytes)} · {FormatShare(
            memory.FragmentedBytes,
            memory.CommittedBytes)}"
        : "—";

    public string MemoryAllocatedValue => _memory.Value is { } memory
        ? FormatBytes(memory.TotalAllocatedBytes)
        : "—";

    public string MemoryAllocationRateValue => _memory.Value is { } memory
        ? $"{FormatBytes((long)Math.Max(0, _allocationBytesPerSecond))} / s"
        : "—";

    public string MemoryWorkingSetValue => _memory.Value is { } memory
        ? FormatBytes(memory.WorkingSetBytes)
        : "—";

    public string MemoryPrivateValue => _memory.Value is { } memory
        ? FormatBytes(memory.PrivateBytes)
        : "—";

    public string MemoryPauseValue => _memory.Value is { } memory
        ? $"{memory.PauseTimePercentage:0.0} %"
        : "—";

    public string MemoryGen0Value => _memory.Value is { } memory
        ? FormatBytes(memory.Gen0Bytes)
        : "—";

    public string MemoryGen1Value => _memory.Value is { } memory
        ? FormatBytes(memory.Gen1Bytes)
        : "—";

    public string MemoryGen2Value => _memory.Value is { } memory
        ? FormatBytes(memory.Gen2Bytes)
        : "—";

    public string MemoryCollectionsValue => _memory.Value is { } memory
        ? $"g0 {memory.Gen0Collections:N0} · g1 {memory.Gen1Collections:N0} · " +
          $"g2 {memory.Gen2Collections:N0}"
        : "—";

    public string MemorySampleValue => _memory.Value is null
        ? "waiting for a sample"
        : $"sampled {_memorySampleAt?.ToString("HH:mm:ss") ?? "—"}";

    private void SampleMemory(bool force = false) {
        if (!force && !ShowMemory) {
            return;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (!force &&
            timestamp - _lastMemorySampleTimestamp < MemorySampleIntervalTicks) {
            return;
        }

        var statistics = MemoryStatistics.Capture();
        if (_lastMemorySampleTimestamp != 0) {
            var elapsedSeconds =
                (timestamp - _lastMemorySampleTimestamp) / (double)Stopwatch.Frequency;
            var allocatedBytes =
                statistics.TotalAllocatedBytes - _lastAllocatedBytes;
            if (elapsedSeconds > 0 && allocatedBytes >= 0) {
                _allocationBytesPerSecond = allocatedBytes / elapsedSeconds;
            }
        }

        _lastMemorySampleTimestamp = timestamp;
        _lastAllocatedBytes = statistics.TotalAllocatedBytes;
        _peakHeapBytes = Math.Max(_peakHeapBytes, statistics.ManagedHeapBytes);
        _heapHistory[_heapHistoryHead] = statistics.ManagedHeapBytes;
        _heapHistoryHead = (_heapHistoryHead + 1) % HeapHistoryLength;
        _heapHistoryCount = Math.Min(_heapHistoryCount + 1, HeapHistoryLength);
        _memorySampleAt = DateTime.Now;
        _memory.Value = statistics;
        _heapChart?.InvalidatePaint();
    }

    private void OnMemoryProbeTick() => SampleMemory();

    private void AttachMemoryProbe(Element root) {
        var probe = root.FindFirst<MemoryProbe>();
        if (ReferenceEquals(probe, _memoryProbe)) {
            return;
        }

        if (_memoryProbe is not null) {
            _memoryProbe.Tick -= OnMemoryProbeTick;
        }

        _memoryProbe = probe;
        if (_memoryProbe is not null) {
            _memoryProbe.Tick += OnMemoryProbeTick;
        }
    }

    private void AttachHeapChart(Element root) {
        var chart = root
            .FindAll<Canvas>()
            .FirstOrDefault(canvas => canvas.Id == HeapChartId);
        if (ReferenceEquals(chart, _heapChart)) {
            return;
        }

        _heapChart = chart;
        if (_heapChart is not null) {
            _heapChart.Draw = _heapChartDraw;
        }
    }

    private void DrawHeapChart(
        RenderCommandContext context,
        List<RenderCommand> commands) {
        if (_heapHistoryCount == 0) {
            return;
        }

        var bounds = context.Bounds;
        var padding = context.Padding;
        var left = bounds.X + padding;
        var top = bounds.Y + padding;
        var width = Math.Max(1, bounds.Width - (padding * 2));
        var height = Math.Max(1, bounds.Height - (padding * 2));
        var oldest = _heapHistoryHead - _heapHistoryCount;
        if (oldest < 0) {
            oldest += HeapHistoryLength;
        }

        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        for (var index = 0; index < _heapHistoryCount; index++) {
            var value = _heapHistory[(oldest + index) % HeapHistoryLength];
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        var range = maximum - minimum;
        var flat = range <= maximum * 0.02;
        var slot = width / HeapHistoryLength;
        var offset = HeapHistoryLength - _heapHistoryCount;
        var baseline = top + height;
        var thickness = 2f;
        var span = MathF.Max(1f, height - thickness);
        float? previousTop = null;
        for (var index = 0; index < _heapHistoryCount; index++) {
            var value = _heapHistory[(oldest + index) % HeapHistoryLength];
            var normalized = flat
                ? 0.5f
                : (float)Math.Clamp((value - minimum) / range, 0, 1);
            var y = baseline - thickness - (normalized * span);
            var columnTop = previousTop is { } previous
                ? MathF.Min(previous, y)
                : y;
            var columnBottom = previousTop is { } previousBottom
                ? MathF.Max(previousBottom, y) + thickness
                : y + thickness;
            commands.Add(new FillRectangleCommand(
                new Rect(
                    left + ((offset + index) * slot),
                    columnTop,
                    slot,
                    MathF.Max(thickness, columnBottom - columnTop)),
                context.Foreground));
            previousTop = y;
        }
    }

    private static string FormatBytes(long bytes) {
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < ByteUnits.Length - 1) {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {ByteUnits[unit]}"
            : $"{value:0.0} {ByteUnits[unit]}";
    }

    private static string FormatShare(long part, long total) =>
        total <= 0 ? "0 %" : $"{100d * part / total:0} %";
}
