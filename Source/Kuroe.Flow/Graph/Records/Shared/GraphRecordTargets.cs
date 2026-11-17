using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Flow.Graph.Records.Shared;

/// <summary>图节点路径与中性执行目标标识的记录转换，编码由节点路径转换统一解释。</summary>
public static class GraphRecordTargets
{
    /// <summary>把节点路径写成执行目标标识。</summary>
    public static ExecutionTargetId Format(NodePath path) => ExecutionTargetPaths.Target(path);

    /// <summary>把执行目标标识还原为节点路径。</summary>
    public static NodePath Parse(ExecutionTargetId target) => ExecutionTargetPaths.Parse(target);
}