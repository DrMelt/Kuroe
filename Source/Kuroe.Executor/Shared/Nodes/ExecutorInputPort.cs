using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器条目的输入端口声明：名字、类型、模式、说明、默认值与执行角色。类型决定动态来源产出如何进入本条目。
/// 未写类型时取 Text，未写模式时取端口类型的默认模式。未写角色时，常量端口按配置，动态端口按内容。</summary>
public readonly record struct ExecutorInputPort
{
    /// <summary>声明一个输入端口。</summary>
    public ExecutorInputPort(
        PortName name,
        PortType? type = null,
        string? description = null,
        string? defaultValue = null,
        PortMode? mode = null,
        InputRole? role = null)
    {
        Name = name;
        Type = type ?? PortTypes.Text;
        Description = description;
        Default = defaultValue;
        Mode = mode ?? PortTypes.DefaultMode(Type);
        Role = role ?? DefaultRole(Mode, Type);
    }

    /// <summary>端口名。</summary>
    public PortName Name { get; init; }

    /// <summary>端口类型。</summary>
    public PortType Type { get; init; }

    /// <summary>端口模式。</summary>
    public PortMode Mode { get; init; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; init; }

    /// <summary>默认值，未写时为空。</summary>
    public string? Default { get; init; }

    /// <summary>执行角色：内容、配置或控制。</summary>
    public InputRole Role { get; init; }

    /// <summary>常量模式的静态输入。</summary>
    public bool IsStatic => Mode == PortMode.Constant;

    /// <summary>动态绑定是否使用来源触发值门控启动组。</summary>
    public bool Trigger => Mode == PortMode.Trigger;

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;

    private static InputRole DefaultRole(PortMode mode, PortType type)
    {
        if (mode == PortMode.Constant)
        {
            return InputRole.Configuration;
        }

        return type.Semantics == PortSemantics.Signal ? InputRole.Control : InputRole.Content;
    }
}