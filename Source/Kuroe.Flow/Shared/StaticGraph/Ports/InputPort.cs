using Kuroe.Flow.Shared.Model;
namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>输入端口声明：名字、类型、说明、默认值与静态标记。类型决定来源产出如何进入本节点。
/// 容器与执行节点共用，容器由引用处绑定，执行节点由自身 From 绑定。
/// 静态输入由 Static 绑定提供值，不进入 From、版本、就绪与触发；默认值只在 Text 输入端口上生效。</summary>
public readonly record struct InputPort
{
    /// <summary>声明一个输入端口，未写类型时取普通 Text。</summary>
    public InputPort(
        PortName name,
        PortType? type = null,
        string? description = null,
        string? defaultValue = null,
        bool isStatic = false)
    {
        Name = name;
        Type = type ?? PortTypes.Text;
        Description = description;
        Default = defaultValue;
        IsStatic = isStatic;
    }

    /// <summary>端口名。</summary>
    public PortName Name { get; init; }

    /// <summary>端口类型。</summary>
    public PortType Type { get; init; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; init; }

    /// <summary>默认值，未写时为空。</summary>
    public string? Default { get; init; }

    /// <summary>静态输入端口：值来自 Static 绑定或默认值，不进入 From、版本、就绪与触发。</summary>
    public bool IsStatic { get; init; }

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
