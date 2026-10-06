namespace Kuroe.Shared.Executions.Tools;

/// <summary>模型可调用的函数名，工具注册按它排重，模型按它调用。展示即原文。</summary>
public readonly record struct ToolName(string Value)
{
    /// <summary>规划执行节点交回条目拆分的契约工具。</summary>
    public static ToolName ContractPlan { get; } = new("SubmitPlanItems");

    /// <summary>声明输出端口的执行节点交回命名端口产出的契约工具。</summary>
    public static ToolName ContractPortValues { get; } = new("SubmitPortValues");

    /// <summary>函数名的文本形式。</summary>
    public override string ToString() => Value;
}
