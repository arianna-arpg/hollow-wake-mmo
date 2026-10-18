namespace HollowWake.Tools;

/// <summary>Two allocation counters over one synchronous invocation, without subtracting an estimated overhead.</summary>
public readonly record struct AllocationSample(long ProcessBytes, long CallingThreadBytes)
{
    /// <summary>
    /// The caller constructs and warms <paramref name="work"/> before sampling. Work is invoked exactly once;
    /// this helper does not warm it, retry it or select a minimum. The process counter includes other threads.
    /// Runtime bookkeeping on the calling thread remains visible in the calling-thread counter.
    /// </summary>
    public static AllocationSample Capture(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _ = GC.GetAllocatedBytesForCurrentThread();
        var processBefore = GC.GetTotalAllocatedBytes(true);
        var threadBefore = GC.GetAllocatedBytesForCurrentThread();
        work();
        var threadBytes = GC.GetAllocatedBytesForCurrentThread() - threadBefore;
        var processBytes = GC.GetTotalAllocatedBytes(true) - processBefore;
        return new AllocationSample(processBytes, threadBytes);
    }
}
