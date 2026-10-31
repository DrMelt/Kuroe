using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>一棵流程树展平后的一个执行节点及其解析结果。类型给出执行方式，只有形状声明派发执行的类型才真正启动执行。</summary>
public sealed record ExecutableNode : GraphNode
{
    /// <summary>节点类型的描述实例，类型专属声明封在它里面。</summary>
    public required INodeType Type { get; init; }

    /// <summary>节点类型名。</summary>
    public NodeKind Kind => Type.Kind;

    /// <summary>执行配置：输出校验与执行上限，编译期只把执行上限落定为生效值。</summary>
    public required ExecutableSpec Spec { get; init; }

    /// <summary>静态输入端口到来源端口的绑定。来源在运行期沿绑定链解析，编译期只校验与落定绑定。</summary>
    public IReadOnlyDictionary<PortName, NodePort>? StaticBindings { get; init; }

    /// <summary>执行节点的输入端口表：名字、类型、默认值与静态标记，类型决定来源产出如何进入本节点。</summary>
    public override IReadOnlyList<InputPort> Inputs { get; init; } = [];

    /// <summary>执行节点的输出端口表：名字、类型与来源。常量输出由固定值、静态输入来源或 Once 节点唯一一次执行提供。</summary>
    public override IReadOnlyList<Port> Outputs { get; init; } = [];

    /// <summary>输出端口到内部来源端口的绑定：常量输出来源是本节点的静态输入。未声明来源时为空。</summary>
    public override IReadOnlyDictionary<PortName, NodePort>? OutputBindings { get; init; }

    /// <summary>执行次数的生效上限，配置链未声明时由编译落定为默认值。审批节点不启动执行，不限次数。</summary>
    public required int MaxExecutions { get; init; }

    /// <summary>汇入本节点的边，编译期落定，按编译顺序。输入侧方案与启动判定按它读数。</summary>
    public IReadOnlyList<FlowEdge> Incoming { get; init; } = [];

    /// <summary>执行次数上限的默认值，配置链未声明时由编译落定。</summary>
    public const int DefaultMaxExecutions = 100;

    /// <summary>需经契约工具交回的输出端口：动态 Text 与 Signal 端口，以及由执行产出的常量端口。</summary>
    public IReadOnlyList<Port> RequiredPorts => PortRules.RequiringSubmission(Outputs, Spec.Emission);

    /// <summary>执行可交回的输出端口：全部动态端口与由执行产出的常量端口。</summary>
    public IReadOnlyList<Port> SubmittablePorts => PortRules.SubmittablePorts(Outputs, Spec.Emission);
}
