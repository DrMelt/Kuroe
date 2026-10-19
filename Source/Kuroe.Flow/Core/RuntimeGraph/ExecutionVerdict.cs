namespace Kuroe.Flow.RuntimeGraph;

/// <summary>模型对某一轮执行声明的结论：交付完毕或无法完成。无法完成时带原因。</summary>
/// <param name="Blocked">无法完成时置真。</param>
/// <param name="Reason">无法完成的原因，交付完毕时为空。</param>
public sealed record ExecutionVerdict(bool Blocked, string? Reason)
{
    /// <summary>本轮交付完毕。</summary>
    public static ExecutionVerdict Done { get; } = new(false, null);

    /// <summary>本轮无法完成。</summary>
    public static ExecutionVerdict CannotComplete(string reason) => new(true, reason);
}
