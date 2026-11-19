using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器条目的输出端口声明：名字、类型、模式、说明、固定值与默认值。
/// 未写模式时取端口类型的默认模式。默认值只用于承载内容的开始端口。</summary>
public readonly record struct ExecutorOutputPort
{
    /// <summary>声明一个输出端口。</summary>
    public ExecutorOutputPort(
        PortName name,
        PortType type,
        string? description = null,
        string? value = null,
        PortMode? mode = null,
        string? defaultValue = null)
    {
        Name = name;
        Type = type;
        Description = description;
        Value = value;
        Mode = mode ?? PortTypes.DefaultMode(type);
        Default = defaultValue;
    }

    /// <summary>端口名。</summary>
    public PortName Name { get; init; }

    /// <summary>端口类型。</summary>
    public PortType Type { get; init; }

    /// <summary>端口模式。</summary>
    public PortMode Mode { get; init; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; init; }

    /// <summary>固定值，常量端口使用。</summary>
    public string? Value { get; init; }

    /// <summary>承载内容的开始端口的提交默认值，未写时为空。</summary>
    public string? Default { get; init; }

    /// <summary>动态产出是否携带触发值。</summary>
    public bool Trigger => Mode == PortMode.Trigger;

    /// <summary>是否为常量端口。</summary>
    public bool IsConstant => Mode == PortMode.Constant;

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}