using ErrorOr;

namespace Kuroe.Shared.Workflows.Flows;

/// <summary>执行节点的命名输入或输出端口名。展示即原文。</summary>
public readonly record struct PortName(string Value)
{
    /// <summary>解析端口名，拒绝空、全空白或含 @ 的文本。</summary>
    public static ErrorOr<PortName> Create(string name) =>
        string.IsNullOrWhiteSpace(name) || name.Contains('@')
            ? [Error.Validation(ErrorCodes.FlowNode, "端口名不能为空或含 @。")]
            : new PortName(name.Trim());

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Value;
}
