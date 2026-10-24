namespace Kuroe.Flow.Shared.StaticGraph.PortModel;

/// <summary>输入端口声明：名字、类型、说明与默认值。类型决定来源产出如何进入本节点。
/// 容器与执行节点共用，容器由引用处绑定，执行节点由自身 From 绑定。
/// 默认值只在派发执行节点的普通 Text 输入端口上生效，来源组没有连接该端口时提供内容。</summary>
public readonly record struct InputPort
{
    /// <summary>声明一个输入端口，未写类型时取普通 Text。</summary>
    public InputPort(
        PortName name,
        PortType? type = null,
        string? description = null,
        string? defaultValue = null)
    {
        Name = name;
        Type = type ?? PortTypes.Text;
        Description = description;
        Default = defaultValue;
    }

    /// <summary>端口名。</summary>
    public PortName Name { get; init; }

    /// <summary>端口类型。</summary>
    public PortType Type { get; init; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; init; }

    /// <summary>默认值，未写时为空。</summary>
    public string? Default { get; init; }

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
