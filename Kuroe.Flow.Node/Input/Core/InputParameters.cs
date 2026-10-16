namespace Kuroe.Flow.Node.Input;

/// <summary>输入节点类型的声明：等待用户回答时的提示文本，由输入类型实例承载。</summary>
public sealed record InputParameters
{
    /// <summary>等待外部输入时的提示文本，未写时用通用提示。</summary>
    public string? Question { get; init; }
}