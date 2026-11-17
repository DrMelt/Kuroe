namespace Kuroe.Execution.Shared.Runtime;

/// <summary>执行运行器键，用于选择执行器工厂与恢复器。</summary>
public readonly record struct ExecutionRunnerKey(string Value)
{
    /// <summary>执行运行器键的文本形式。</summary>
    public override string ToString() => Value;
}