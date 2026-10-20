namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>一棵流程树展平后的一个执行节点及其解析结果。类型给出执行方式，只有形状声明派发执行的类型才真正启动执行。</summary>
public sealed record ExecutableNode : GraphNode
{
    /// <summary>节点类型的描述实例，类型专属声明封在它里面。</summary>
    public required INodeType Type { get; init; }

    /// <summary>节点类型名。</summary>
    public NodeKind Kind => Type.Kind;

    /// <summary>执行配置：输出校验与执行上限，编译期只把执行上限落定为生效值。</summary>
    public required ExecutableSpec Spec { get; init; }

    /// <summary>引用的模型选择解析结果：输入节点不启动执行，为空。</summary>
    public required ModelDefinition? Model { get; init; }

    /// <summary>执行节点的输出端口表：名字与类型，类型决定产出形态与消费方式。</summary>
    public required IReadOnlyList<Port> Outputs { get; init; }

    /// <summary>执行次数的生效上限，配置链未声明时由编译落定为默认值。审批节点不启动执行，不限次数。</summary>
    public required int MaxExecutions { get; init; }

    /// <summary>汇入本节点的边，编译期落定，按编译顺序。输入侧方案与启动判定按它读数。</summary>
    public IReadOnlyList<FlowEdge> Incoming { get; init; } = [];

    /// <summary>输入侧方案，由自身声明与入边算出。不需来源即可挂起的类型没有方案，它们不按就绪判定。</summary>
    public NodeInputPlan? Plan => Type.Shape.StartsUnaided ? null : NodeInputPlan.Of(Spec, Incoming);

    /// <summary>执行次数上限的默认值，配置链未声明时由编译落定。</summary>
    public const int DefaultMaxExecutions = 100;

    /// <summary>需经契约工具交回的输出端口：Text 与 Signal，未声明即不要求模型交回。</summary>
    public IReadOnlyList<Port> RequiredPorts => Ports.RequiringSubmission(Outputs);

    /// <summary>按名字取端口声明，未声明时为空。</summary>
    public Port? FindPort(PortName name) => Outputs.Find(name);
}
