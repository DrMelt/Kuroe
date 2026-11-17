using Kuroe.Flow.Shared.Model;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Identity;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;

/// <summary>图节点路径与执行目标标识的转换。路径段按 URI 规则转义，段之间用 / 分隔。</summary>
public static class ExecutionTargetPaths
{
    /// <summary>把节点路径写成执行目标标识。</summary>
    public static ExecutionTargetId Target(NodePath path) =>
        new(string.Join('/', path.Levels.Select(level => Uri.EscapeDataString(level.Value))));

    /// <summary>把执行目标标识还原为节点路径。</summary>
    public static NodePath Parse(ExecutionTargetId target)
    {
        string[] levels = target.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (levels.Length == 0)
        {
            throw new FormatException($"执行目标 {target.Value} 不是节点路径。");
        }

        return new NodePath([.. levels.Select(level => new NodeLabel(Uri.UnescapeDataString(level)))]);
    }

    /// <summary>把执行目标标识还原为流程步骤路径。</summary>
    public static FlowStepPath StepPath(ExecutionTargetId target) => NodeStepPaths.From(Parse(target));
}