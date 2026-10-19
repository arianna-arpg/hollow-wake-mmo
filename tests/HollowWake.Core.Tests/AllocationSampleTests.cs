using HollowWake.Tools;
using Xunit;

namespace HollowWake.Core.Tests;

public class AllocationSampleTests
{
    private static object? _escaped;

    [Fact]
    public void SamplesWorkExactlyOnceWithoutAnImplicitWarmupOrRetry()
    {
        var calls = 0;
        AllocationSample.Capture(() => calls++);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CallingThreadCounterIncludesRealEscapingAllocations()
    {
        const int payloadBytes = 4096;
        Action work = () => Volatile.Write(ref _escaped, new byte[payloadBytes]);
        work();
        var sample = AllocationSample.Capture(work);
        Assert.True(sample.CallingThreadBytes >= payloadBytes, $"calling thread counted only {sample.CallingThreadBytes} bytes");
        Assert.True(sample.ProcessBytes >= sample.CallingThreadBytes);
    }

    [Fact]
    public void ProcessCounterIncludesAllocationsOnAnotherThread()
    {
        const int payloadBytes = 1024 * 1024;
        using var ready = new ManualResetEventSlim();
        using var start = new ManualResetEventSlim();
        using var done = new ManualResetEventSlim();
        var worker = new Thread(() =>
        {
            ready.Set();
            if (!start.Wait(5000)) return;
            Volatile.Write(ref _escaped, new byte[payloadBytes]);
            done.Set();
        }) { IsBackground = true };
        worker.Start();
        try
        {
            Assert.True(ready.Wait(5000));
            var sample = AllocationSample.Capture(() =>
            {
                start.Set();
                if (!done.Wait(5000)) throw new TimeoutException("Allocation worker did not complete");
            });
            Assert.True(sample.ProcessBytes >= payloadBytes, $"process counted only {sample.ProcessBytes} bytes");
            // Synchronization/runtime bookkeeping may allocate locally; it must not include the worker's array.
            Assert.True(sample.CallingThreadBytes < payloadBytes / 4, $"calling thread counted {sample.CallingThreadBytes} bytes");
        }
        finally
        {
            start.Set();
            Assert.True(worker.Join(5000));
        }
    }
}
