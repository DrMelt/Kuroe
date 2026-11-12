using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Presentation;

/// <summary>展示用执行节点：路径、标签、种类、端口与执行上限。只含只读声明，不含节点运行期。</summary>
public sealed record FlowExecutableView(
    NodePath Path,
    NodeLabel Label,
    NodeKind Kind,
    INodeType Type,
    ExecutableSpec Spec,
    IReadOnlyList<InputPort> Inputs,
    IReadOnlyList<Port> Outputs,
    IReadOnlyDictionary<PortName, OutputBinding>? OutputBindings,
    IReadOnlyList<InputBinding> Incoming,
    int MaxExecutions)
{
    /// <summary>需经契约工具交回的输出端口。</summary>
    public IReadOnlyList<Port> RequiredPorts => PortRules.RequiringSubmission(Outputs, Spec.Emission);

    /// <summary>执行可交回的输出端口。</summary>
    public IReadOnlyList<Port> SubmittablePorts => PortRules.SubmittablePorts(Outputs, Spec.Emission);
}