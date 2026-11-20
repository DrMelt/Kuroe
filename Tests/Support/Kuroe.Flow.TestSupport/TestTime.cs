namespace Kuroe.Flow.TestSupport;

/// <summary>测试用固定时刻，避免用例读取真实时钟。</summary>
public static class TestTime
{
    /// <summary>固定时刻。</summary>
    public static readonly DateTimeOffset At = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
}