using ErrorOr;
using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Submissions;
using Kuroe.Flow.RuntimeNodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Execution.Tasks;

/// <summary>模型侧交回结构化产出的入口：规划交条目拆分。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class WorkItemSubmitter(TaskRegistry registry)
{
    /// <summary>声明 WorkItems 端口的执行节点交回条目拆分。引用它的执行节点声明了分支时，交回的条目都要落在这些分支上。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitItems(RunId id, string itemsJson)
    {
        ErrorOr<(Run Run, WorkTask Task)> owner = SubmitterContext.Resolve(registry, id, "提交条目拆分");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        (Run run, WorkTask task) = owner.Value;
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
                if (edge.Role == EdgeKind.Split && task.Graph[edge.Target] is ExecutableNode { Execution.Branch: { } branch })
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
}

