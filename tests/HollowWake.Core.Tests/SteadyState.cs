using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Asserts that a piece of work allocates nothing on the calling thread once the runtime has settled on it.
/// </summary>
/// <remarks>
/// Reading <see cref="GC.GetAllocatedBytesForCurrentThread"/> around one loop measures more than the loop.
/// Tiered compilation runs every method unoptimized first and replaces it later: optimized code is compiled on
/// a background thread once a method has been called enough times, or on the calling thread itself when a
/// method is caught in a hot loop (on-stack replacement), and the runtime's own bookkeeping at those
/// transitions can allocate on the thread that triggers them. When a transition falls inside the measured
/// section, the counter reads a few kilobytes the work never allocated. On an idle machine the transitions
/// are over before the measurement; on a starved one (the shared CI runner, or this suite pinned to four
/// cores) one lands inside it in one run of several. The counter itself is exact for its thread: a collection
/// on another thread neither adds nor removes bytes from it.
/// <para>
/// So the work runs unmeasured until the runtime's call-count threshold is behind it, then in measured rounds.
/// The claim holds as soon as one round allocates nothing: an allocation the work makes itself repeats in every
/// round, while the runtime's one-off lands in a single round. When every round allocates, the failure lists
/// them all.
/// </para>
/// </remarks>
internal static class SteadyState
{
    /// <summary>Unmeasured calls before the rounds: more than the thirty calls after which the runtime promotes a method.</summary>
    public const int WarmUpCalls = 40;

    /// <summary>
    /// Runs <paramref name="work"/> <paramref name="warmUpCalls"/> times unmeasured, then in up to <paramref name="rounds"/>
    /// measured rounds of <paramref name="callsPerRound"/> calls, and fails unless some round allocates nothing on this thread.
    /// </summary>
    public static void AssertAllocationFree(Action work, int callsPerRound, int rounds = 8, int warmUpCalls = WarmUpCalls)
    {
        for (var i = 0; i < warmUpCalls; i++)
        {
            work();
        }
        var allocated = new long[rounds];
        for (var round = 0; round < rounds; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < callsPerRound; i++)
            {
                work();
            }
            allocated[round] = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated[round] == 0)
            {
                return;
            }
        }
        Assert.Fail($"every round of {callsPerRound} calls allocated; bytes per round: {string.Join(", ", allocated)}");
    }
}
