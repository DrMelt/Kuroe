namespace Kuroe.Flow.TestSupport;

/// <summary>测试等待的上限，只用于把通知丢失或死锁转成失败，不参与通过判定。</summary>
internal static class TestTimeouts
{
    /// <summary>等待上限。</summary>
    public static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(60);
}