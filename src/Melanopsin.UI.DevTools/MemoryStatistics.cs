using System.Diagnostics;

namespace Melanopsin.UI.DevTools;

/// <summary>
/// A point-in-time sample of managed heap and process memory counters. Sampling
/// never forces a collection, so values track ordinary allocation activity.
/// </summary>
public readonly record struct MemoryStatistics(
    long ManagedHeapBytes,
    long CommittedBytes,
    long FragmentedBytes,
    long AvailableBytes,
    long WorkingSetBytes,
    long PrivateBytes,
    long TotalAllocatedBytes,
    long Gen0Bytes,
    long Gen1Bytes,
    long Gen2Bytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    double PauseTimePercentage) {
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    /// <summary>
    /// Samples the current process without forcing a garbage collection.
    /// </summary>
    public static MemoryStatistics Capture() {
        var info = GC.GetGCMemoryInfo();
        return new MemoryStatistics(
            info.HeapSizeBytes,
            info.TotalCommittedBytes,
            info.FragmentedBytes,
            info.TotalAvailableMemoryBytes,
            Environment.WorkingSet,
            ReadPrivateBytes(),
            GC.GetTotalAllocatedBytes(precise: false),
            GenerationBytes(info, 0),
            GenerationBytes(info, 1),
            GenerationBytes(info, 2),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            info.PauseTimePercentage);
    }

    private static long GenerationBytes(GCMemoryInfo info, int generation) {
        var generations = info.GenerationInfo;
        return generation < generations.Length
            ? generations[generation].SizeAfterBytes
            : 0;
    }

    private static long ReadPrivateBytes() {
        try {
            return CurrentProcess.PrivateMemorySize64;
        } catch (Exception) {
            // The counter can be unavailable on constrained platforms.
            return 0;
        }
    }
}
