using ErrorOr;
using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.Execution.Model;
using Kuroe.Flows.RuntimeNodes;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flows.Execution.Tasks;

/// <summary>模型侧交回结构化产出的入口：规划交条目拆分。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class WorkItemSubmitter(TaskRegistry registry)
{
    /// <summary>声明 WorkItems 端口的执行节点交回条目拆分。引用它的执行节点声明了分支时，交回的条目都要落在这些分支上。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitItems(RunId id, string itemsJson)
    {
        if (Owner(id) is not { } run)
        {
            return Error.Validation("Tool.Argument", "只有进行中的 run 能提交条目拆分。");
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return Error.Unexpected("Tool.Internal", "该 run 没有归属任务。");
        }

        WorkTask task = found.Value;
        ErrorOr<IReadOnlyList<WorkItem>> parsed = WorkItemParser.Parse(itemsJson);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        lock (task.Gate)
        {
            RuntimeExecutable node = task.Runtime.Executable(run.Context.NodeIndex);
            if (!node.Executable.HasWorkItemsPort)
            {
                return Error.Validation("Tool.Argument", "该执行节点没有声明 WorkItems 端口，不能提交条目拆分。");
            }

            if (node.Split is { } existing && existing.Origin == run.Id)
            {
                return "本轮已提交过条目拆分，无需重复提交。";
            }

            List<BranchName> branches = [];
            foreach (FlowEdge edge in task.Graph.Outgoing(run.Context.NodeIndex))
            {
                if (edge.Kind == EdgeKind.Split && task.Graph[edge.Target] is ExecutableNode { Execution.Branch: { } branch })
                {
                    branches.Add(branch);
                }
            }

            if (branches.Count > 0)
            {
                foreach (BranchName? branch in parsed.Value.Select(item => item.Branch))
                {
                    if (branch is not { } name)
                    {
                        return Error.Validation("Tool.Argument", "分流任务的条目必须写明分支 Branch。");
                    }

                    if (!branches.Contains(name))
                    {
                        return Error.Validation("Tool.Argument", $"分支 {name} 没有对应的分支执行节点。");
                    }
                }
            }

            node.SubmitSplit(new WorkItemList(run.Id, parsed.Value));

            return $"已记录 {parsed.Value.Count} 个条目。{ContractText.Outstanding(task, run)}";
        }
    }

    /// <summary>提交者身份必须是仍在跑的 run。</summary>
    private Run? Owner(RunId id) =>
        registry.FindRun(id) is { IsError: false } found && found.Value.IsLive
            ? found.Value
            : null;
}

