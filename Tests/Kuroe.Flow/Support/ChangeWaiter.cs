namespace Kuroe.Flow.TestSupport;

/// <summary>把变更通知与条件判定结合：先记版本，判定条件，再等下一次变更，避免判定与挂起之间漏唤醒。
/// 等待上限只把通知丢失或死锁转成失败，不参与通过判定。</summary>
public sealed class ChangeWaiter
{
    private readonly ChangeSignal _signal = new();

    /// <summary>播发一次变更，供事件订阅调用。</summary>
    public void Raise() => _signal.Raise();

    /// <summary>等到条件成立。</summary>
    public Task UntilAsync(Func<bool> condition) =>
        UntilAsync(condition, TestTimeouts.HangGuard);

    /// <summary>等到条件成立，超过上限视为通知丢失。</summary>
    public async Task UntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        while (true)
        {
            long version = _signal.Version;
            if (condition())
            {
                return;
            }

            try
            {
                await _signal.WaitAsync(version).WaitAsync(timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"条件在 {timeout.TotalSeconds} 秒内没有成立。");
            }
        }
    }
}