using Kuroe.FlowDescription.Shared.Model;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;

/// <summary>图节点路径与执行步骤标识的转换。路径段按 URI 规则转义，段之间用 / 分隔。</summary>
public static class ExecutionStepPaths
{
    /// <summary>把节点路径写成执行步骤标识。</summary>
    public static ExecutionStepId From(NodePath path) =>
        new(string.Join('/', path.Levels.Select(level => Uri.EscapeDataString(level.Value))));

    /// <summary>把执行步骤标识还原为节点路径。</summary>
    public static NodePath ToNodePath(ExecutionStepId step)
    {
        string[] levels = step.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (levels.Length == 0)
        {
            throw new FormatException($"执行步骤 {step.Value} 不是节点路径。");
        }

        return new NodePath([.. levels.Select(level => new NodeLabel(Uri.UnescapeDataString(level)))]);
    }

    /// <summary>把执行步骤标识还原为流程步骤路径。</summary>
    public static FlowStepPath ToFlowStepPath(ExecutionStepId step) => NodeStepPaths.From(ToNodePath(step));
}