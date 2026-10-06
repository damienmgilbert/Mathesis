namespace Mathesis.Testing;

/// <summary>
/// Measures how many bytes a block of code allocates on the calling thread, for the allocation test that every hot-path API
/// ships with (docs/design/11-realtime-and-determinism.md): warm up, then count the bytes allocated across many calls.
/// </summary>
public static class Allocation
{
    /// <summary>
    /// Runs <paramref name="body"/> twice to let the JIT and any lazy initialization settle, then once more while counting the
    /// bytes allocated by the current thread. The body should loop over many calls (1e6 in the contract) and use its results, so
    /// the compiler cannot discard them; create delegates and buffers before calling this, not inside the body.
    /// </summary>
    /// <returns>The bytes allocated by the third run; zero for an allocation-free body.</returns>
    public static long Measure(Action body)
    {
        ArgumentNullException.ThrowIfNull(body);
        body();
        body();
        var before = GC.GetAllocatedBytesForCurrentThread();
        body();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
