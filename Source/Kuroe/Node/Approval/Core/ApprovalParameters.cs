namespace Kuroe.Node.Approval;

/// <summary>审批节点类型的声明：等待用户批准时的提示文本，由审批类型实例承载。</summary>
public sealed record ApprovalParameters
{
    /// <summary>等待外部输入时的提示文本，未写时用通用提示。</summary>
    public string? Question { get; init; }
}