using Kuroe.Flow.Shared.Files;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Approval;

/// <summary>审批节点类型的参数：等待用户批准时的提示文本。</summary>
public sealed record ApprovalParameters : INodeParameters
{
    /// <summary>等待外部输入时的提示文本，未写时用通用提示。</summary>
    public string? Question { get; init; }
}