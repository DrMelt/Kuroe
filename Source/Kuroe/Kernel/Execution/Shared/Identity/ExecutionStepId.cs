namespace Kuroe.Kernel.Execution.Shared.Identity;

/// <summary>执行步骤标识。由流程控制生成并解释，执行核心只透传。</summary>
public readonly record struct ExecutionStepId(string Value)
{
    /// <summary>执行步骤标识的文本形式。</summary>
    public override string ToString() => Value;
}
