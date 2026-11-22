namespace Kuroe.Flow.TestSupport;

/// <summary>可控时钟桩：给出固定时刻并可推进，避免用例读取真实时钟。</summary>
public sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>推进到给定时刻。</summary>
    public void Set(DateTimeOffset now) => _now = now;
}
