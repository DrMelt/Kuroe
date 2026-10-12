namespace Kuroe.Shared.Workflows.Flows;

/// <summary>输入端口声明：名字、类型与说明。类型决定来源产出如何进入本节点。
/// 容器与执行节点共用，容器由引用处绑定，执行节点由自身 From 绑定。</summary>
public readonly record struct InputPort(PortName Name, PortType Type = PortType.Data, string? Description = null)
{
    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
