using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>模型侧交回结构化产出的入口：规划交条目拆分。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class PlanSubmitter(TaskRegistry registry)
{
    /// <summary>规划执行节点交回条目拆分。引用它的执行节点声明了分支时，交回的条目都要落在这些分支上。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitItems(TurnScope? scope, string itemsJson)
    {
        if (Owner(scope) is not { } run || run.Context.Output != NodeOutput.Plan)
        {
            return ToolErrors.Argument("只有进行中的规划 run 能提交条目拆分。");
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return ToolErrors.Internal("该 run 没有归属任务。");
        }

        WorkTask task = found.Value;
        ErrorOr<IReadOnlyList<PlanItem>> parsed = PlanItems.Parse(itemsJson);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        lock (task.Gate)
        {
            if (task.SplitFor(run.Context.NodeIndex) is { } existing && existing.Origin == run.Id)
            {
                return "本轮已提交过条目拆分，无需重复提交。";
            }

            SplitConfig? split = task.Runtime.Executable(run.Context.NodeIndex).Executable.Execution.Split;
            ErrorOr<IReadOnlyList<PlanItem>> merged = split is null
                ? parsed
                : SplitMerge.Apply(split, parsed.Value);
            if (merged.IsError)
            {
                return merged.ErrorsOrEmptyList;
            }

            List<BranchName> branches = [];
            foreach (FlowEdge edge in task.Graph.Outgoing(run.Context.NodeIndex))
            {
                if (edge.Feed == EdgeFeed.Items && task.Graph[edge.To] is ExecutableNode { Execution.Branch: { } branch })
                {
                    branches.Add(branch);
                }
            }

            if (branches.Count > 0)
            {
                foreach (BranchName? branch in merged.Value.Select(item => item.Branch))
                {
                    if (branch is not { } name)
                    {
                        return ToolErrors.Argument("分流任务的条目必须写明分支 Branch。");
                    }

                    if (!branches.Contains(name))
                    {
                        return ToolErrors.Argument($"分支 {name} 没有对应的分支执行节点。");
                    }
                }
            }

            task.SetSplit(run.Context.NodeIndex, new PlanOutput(run.Id, merged.Value));

            return $"已记录 {merged.Value.Count} 个条目。";
        }
    }

    /// <summary>提交者必须绑定了执行回合，且是该契约仍在跑的 run。</summary>
    private Run? Owner(TurnScope? scope) =>
        scope?.Run is { } id
        && registry.FindRun(id) is { IsError: false } found
        && found.Value.IsLive
            ? found.Value
            : null;
}

