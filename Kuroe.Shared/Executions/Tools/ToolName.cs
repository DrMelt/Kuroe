namespace Kuroe.Shared.Executions.Tools;

/// <summary>模型可调用的函数名，流程白名单与工具注册按它匹配。展示即原文。</summary>
public readonly record struct ToolName(string Value)
{
    /// <summary>规划执行节点交回条目拆分的契约工具。</summary>
    public static ToolName ContractPlan { get; } = new("SubmitPlanItems");

    /// <summary>函数名的文本形式。</summary>
    public override string ToString() => Value;
}