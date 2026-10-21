namespace Kuroe.Flow.Shared.StaticGraph.PortModel;

/// <summary>输出端口声明：名字、类型与说明。类型在编译期落定产出形态与消费方式，引用只写名字；
/// 说明写端口承载什么内容、怎么交回，未写时为空。
/// <see cref="Value"/> 是来源端口的固定值，仅开始端口可用，带值时固定输出该值。</summary>
public readonly record struct Port(PortName Name, PortType Type, string? Description = null, string? Value = null)
{
    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
