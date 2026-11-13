namespace Kuroe.Flow.Execution.Shared;

/// <summary>执行目标标识。由流程控制生成并解释，执行单元只透传。</summary>
public readonly record struct ExecutionTargetId(string Value)
{
    /// <summary>执行目标标识的文本形式。</summary>
    public override string ToString() => Value;
}