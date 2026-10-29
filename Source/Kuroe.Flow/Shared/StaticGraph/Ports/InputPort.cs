using Kuroe.Flow.Shared.Model;
namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>输入端口声明：名字、类型、说明、默认值、执行角色与驱动配置。类型决定来源产出如何进入本节点。
/// 容器与执行节点共用，容器由引用处绑定，执行节点由自身 From 绑定。
/// 静态输入由 Static 绑定提供值，不进入 From、版本、就绪与触发；默认值只在 Text 输入端口上生效。
/// 角色不随常量或动态来源改变，执行装配按角色决定内容与配置。</summary>
public readonly record struct InputPort
{
    /// <summary>声明一个输入端口，未写类型时取普通 Text。未写角色时，静态端口按配置，动态端口按内容。</summary>
    public InputPort(
        PortName name,
        PortType? type = null,
        string? description = null,
        string? defaultValue = null,
        bool isStatic = false,
        InputRole? role = null,
        bool? trigger = null)
    {
        Name = name;
        Type = type ?? PortTypes.Text;
        Description = description;
        Default = defaultValue;
        IsStatic = isStatic;
        Role = role ?? DefaultRole(isStatic, Type);
        Trigger = trigger ?? (Type.IsTrigger || Type.Semantics == PortSemantics.Signal);
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

    /// <summary>执行角色：内容、配置或控制。</summary>
    public InputRole Role { get; init; }

    /// <summary>动态绑定时是否使用来源触发值门控启动组。常量输入不使用触发。</summary>
    public bool Trigger { get; init; }

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;

    private static InputRole DefaultRole(bool isStatic, PortType type)
    {
        if (isStatic)
        {
            return InputRole.Configuration;
        }

        return type.Semantics == PortSemantics.Signal ? InputRole.Control : InputRole.Content;
    }
}
