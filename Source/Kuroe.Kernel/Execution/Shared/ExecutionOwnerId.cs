namespace Kuroe.Execution.Shared;

/// <summary>执行归属标识。由流程控制提供，执行单元只透传。</summary>
public readonly record struct ExecutionOwnerId(string Value)
{
    /// <summary>归属标识的文本形式。</summary>
    public override string ToString() => Value;
}