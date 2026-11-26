using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>一个节点的语义解析：节点路径、声明、全部输入来源与输出来源。</summary>
public sealed record FlowSemanticNode(
    NodePath Path,
    NodeSpec Spec,
    IReadOnlyList<FlowSemanticInput> Inputs,
    IReadOnlyDictionary<PortName, FlowSemanticOutput> Outputs)
{
    /// <summary>是否为执行节点。</summary>
    public bool IsExecutable => Spec.Spec is not null && !Spec.IsContainer;

    /// <summary>按端口名取全部来源记录，动态输入可能来自多个启动组。</summary>
    public IEnumerable<FlowSemanticInput> FindInputs(PortName port) =>
        Inputs.Where(input => input.Port.Name == port);

    /// <summary>按端口名取输出来源，未声明时为空。</summary>
    public FlowSemanticOutput? FindOutput(PortName port) =>
        Outputs.GetValueOrDefault(port);
}