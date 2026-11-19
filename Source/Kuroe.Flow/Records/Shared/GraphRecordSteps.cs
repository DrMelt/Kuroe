using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Flow.Records.Shared;

/// <summary>图节点路径与中性执行步骤标识的记录转换，编码由节点路径转换统一解释。</summary>
public static class GraphRecordSteps
{
    /// <summary>把节点路径写成执行步骤标识。</summary>
    public static ExecutionStepId Format(NodePath path) => ExecutionStepPaths.From(path);

    /// <summary>把执行步骤标识还原为节点路径。</summary>
    public static NodePath Parse(ExecutionStepId step) => ExecutionStepPaths.ToNodePath(step);
}