namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>建立恢复计划时交给节点类型的输入：本次执行的输入、来源执行的恢复负载与契约。</summary>
public sealed record RestorePreparationContext(
    ExecutionInput Input,
    ExecutionRestorePayload Payload,
    ExecutionContract Contract);