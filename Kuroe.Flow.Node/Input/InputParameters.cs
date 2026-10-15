using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Input;

/// <summary>输入节点类型的参数：等待用户回答时的提示文本。</summary>
public sealed record InputParameters : INodeParameters
{
    /// <summary>等待外部输入时的提示文本，未写时用通用提示。</summary>
    public string? Question { get; init; }
}