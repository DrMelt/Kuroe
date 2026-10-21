namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>输入端口声明：名字、类型、说明与默认值。类型决定来源产出如何进入本节点。
/// 容器与执行节点共用，容器由引用处绑定，执行节点由自身 From 绑定。
/// 默认值只在派发执行节点的 Text 输入端口上生效，来源组没有连接该端口时提供内容。</summary>
public readonly record struct InputPort(
    PortName Name,
    PortType Type = PortType.Text,
    string? Description = null,
    string? Default = null)
{
    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
