namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>执行标识，跨任务全局递增，按号即可定位。</summary>
public readonly record struct ExecutionId(int Value)
{
    /// <summary>执行标识的文本形式。</summary>
    public override string ToString() => $"执行 #{Value}";
}
