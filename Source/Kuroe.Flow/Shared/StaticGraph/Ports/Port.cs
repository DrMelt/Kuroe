using Kuroe.Flow.Shared.Model;
namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>输出端口声明：名字、类型、模式、说明、来源与触发配置。所有节点的输出都使用这一形状。
/// 类型在编译期落定产出形态与消费方式，引用只写名字；说明写端口承载什么内容、怎么交回，未写时为空。
/// <see cref="Value"/> 是固定值，<see cref="Ref"/> 是同一节点内部作用域中的来源端口，两者互斥。
/// 常量模式是单值端口，值在任务内确定一次，只供 Static 读取；没有固定值与来源引用时由 Once 节点唯一一次执行产出。
/// 动态端口由执行逐次产出或转发动态来源。模式支持范围由端口类型注册声明。</summary>
public readonly record struct Port
{
    /// <summary>声明一个输出端口，未写模式时取端口类型的默认模式。</summary>
    public Port(
        PortName name,
        PortType type,
        string? description = null,
        string? value = null,
        PortRef? reference = null,
        PortMode? mode = null)
    {
        Name = name;
        Type = type;
        Description = description;
        Value = value;
        Ref = reference;
        Mode = mode ?? PortTypes.DefaultMode(type);
    }

    /// <summary>端口名。</summary>
    public PortName Name { get; init; }

    /// <summary>端口类型。</summary>
    public PortType Type { get; init; }

    /// <summary>端口模式，决定产出是否携带触发值与值在任务内确定几次。</summary>
    public PortMode Mode { get; init; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; init; }

    /// <summary>固定值，常量端口使用。</summary>
    public string? Value { get; init; }

    /// <summary>同一节点内部作用域中的来源端口。</summary>
    public PortRef? Ref { get; init; }

    /// <summary>动态产出是否携带触发值供下游启动组门控。</summary>
    public bool Trigger => Mode == PortMode.Trigger;

    /// <summary>是否为常量端口：值在任务内确定一次，只供 Static 读取。</summary>
    public bool IsConstant => Mode == PortMode.Constant;

    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
