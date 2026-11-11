namespace Kuroe.Flow.Execution;

/// <summary>一次执行的本轮契约状态：已交回的执行端口值与已上报的无法完成原因。</summary>
public sealed record ExecutionContract(
    IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue>? Outputs = null,
    string? CannotCompleteReason = null)
{
    /// <summary>没有交回执行端口、没有上报无法完成的契约。</summary>
    public static ExecutionContract Empty { get; } = new();
}