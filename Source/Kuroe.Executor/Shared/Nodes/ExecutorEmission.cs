namespace Kuroe.Executor.Shared.Nodes;

/// <summary>条目产出模式：逐次发布或首次发布后冻结。</summary>
public enum ExecutorEmission
{
    /// <summary>每次执行都发布一次产出，后续执行覆盖前一次。</summary>
    Repeated,

    /// <summary>首次成功发布后冻结，不再执行；未发布时可以失败重试与重新执行。</summary>
    Once,
}