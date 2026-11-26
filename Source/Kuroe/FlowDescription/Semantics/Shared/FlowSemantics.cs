using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Model;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>一条流程的语言解析结果：流程定义、全部节点解析与诊断。</summary>
public sealed record FlowSemantics(
    FlowDefinition Definition,
    IReadOnlyList<FlowSemanticNode> Nodes,
    IReadOnlyList<FlowSemanticDiagnostic> Diagnostics)
{
    /// <summary>是否没有解析诊断。</summary>
    public bool IsValid => Diagnostics.Count == 0;

    /// <summary>全部执行节点，按先根序。</summary>
    public IEnumerable<FlowSemanticNode> Executables => Nodes.Where(node => node.IsExecutable);

    /// <summary>按路径取节点解析，不存在时为空。</summary>
    public FlowSemanticNode? Find(NodePath path) =>
        Nodes.FirstOrDefault(node => node.Path == path);
}