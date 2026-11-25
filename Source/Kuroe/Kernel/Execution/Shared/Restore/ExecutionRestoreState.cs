namespace Kuroe.Kernel.Execution.Shared.Restore;

/// <summary>一次执行保留的恢复负载快照。格式由执行实现解释。</summary>
public sealed record ExecutionRestoreState(
    string Format,
    string Payload);