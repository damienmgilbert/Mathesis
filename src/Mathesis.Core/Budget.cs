using System.Diagnostics;

namespace Mathesis;

/// <summary>
/// Caps the work an operation may do: rewrite steps, intermediate expression size and wall time, plus cancellation.
/// Exceeding a budget makes engines return <c>Partial</c> or <c>Unevaluated</c>, never hang.
/// </summary>
/// <remarks>
/// The limits are immutable, but consumption (steps used, elapsed time) is tracked on the instance, so create one
/// <see cref="Budget"/> per top-level call. Counting is thread-safe.
/// </remarks>
public sealed class Budget
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private long _stepsUsed;

    /// <summary>Creates a budget. A <c>null</c> limit means unlimited.</summary>
    /// <param name="maxSteps">Maximum number of rewrite steps.</param>
    /// <param name="maxSize">Maximum size (leaf count) of any intermediate expression.</param>
    /// <param name="maxTime">Maximum wall time since creation.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Budget(long? maxSteps = 100_000, int? maxSize = 100_000, TimeSpan? maxTime = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps ?? 0, 0L);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSize ?? 0, 0);
        if (maxTime is { } t) ArgumentOutOfRangeException.ThrowIfLessThan(t, TimeSpan.Zero);
        MaxSteps = maxSteps;
        MaxSize = maxSize;
        MaxTime = maxTime;
        CancellationToken = cancellationToken;
    }

    /// <summary>A shared budget with no limits. Its step counter is still incremented but never trips.</summary>
    public static Budget Unlimited { get; } = new(null, null);

    /// <summary>Maximum number of steps, or <c>null</c> for unlimited.</summary>
    public long? MaxSteps { get; }

    /// <summary>Maximum intermediate expression size, or <c>null</c> for unlimited.</summary>
    public int? MaxSize { get; }

    /// <summary>Maximum wall time, or <c>null</c> for unlimited.</summary>
    public TimeSpan? MaxTime { get; }

    /// <summary>The cancellation token observed by <see cref="IsExceeded"/>.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Steps charged so far.</summary>
    public long StepsUsed => Interlocked.Read(ref _stepsUsed);

    /// <summary>Wall time since the budget was created.</summary>
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <summary>Why the budget is exhausted, or <c>null</c> if it is not.</summary>
    public string? ExceededReason =>
        CancellationToken.IsCancellationRequested ? "Cancelled."
        : MaxSteps is { } s && StepsUsed > s ? $"Step limit of {s} exceeded."
        : MaxTime is { } t && Elapsed > t ? $"Time limit of {t} exceeded."
        : null;

    /// <summary>Whether the step, time or cancellation limit has been hit.</summary>
    public bool IsExceeded => ExceededReason is not null;

    /// <summary>Charges <paramref name="count"/> steps.</summary>
    /// <returns><c>true</c> if work may continue; <c>false</c> once the budget is exceeded.</returns>
    public bool TryCharge(long count = 1)
    {
        Interlocked.Add(ref _stepsUsed, count);
        return !IsExceeded;
    }

    /// <summary>Checks an intermediate expression size against <see cref="MaxSize"/>.</summary>
    /// <returns><c>true</c> if the size is allowed.</returns>
    public bool AllowsSize(int size) => MaxSize is not { } max || size <= max;

    /// <summary>Builds the error to return when this budget is exhausted.</summary>
    public MathError ToError() => MathError.BudgetExceeded(ExceededReason ?? "Budget exceeded.");
}
