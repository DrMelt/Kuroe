using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>模型侧交回结构化产出的唯一入口：规划交条目拆分，检查交结论。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class UnitSubmitter(TaskRegistry registry)
{
    /// <summary>规划执行节点交回条目拆分。引用它的执行节点声明了分支时，交回的条目都要落在这些分支上。</summary>
    public string SubmitPlan(TurnScope? scope, string itemsJson)
    {
        if (Owner(scope) is not { } run || run.Context.Output != NodeOutput.Plan)
        {
            return "被拒绝：只有进行中的规划 run 能提交条目拆分。";
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return "内部错误：该 run 没有归属任务。";
        }

        WorkTask task = found.Value;
        ErrorOr<IReadOnlyList<PlanItem>> parsed = PlanItems.Parse(itemsJson);
        if (parsed.IsError)
        {
            return $"被拒绝：{parsed.FirstError.Description}";
        }

        lock (task.Gate)
        {
            if (task.SplitFor(run.Context.NodeIndex) is { } existing && existing.Origin == run.Id)
            {
                return "本轮已提交过条目拆分，无需重复提交。";
            }

            SplitConfig? split = task.Runtime.Executable(run.Context.NodeIndex).Executable.Split;
            ErrorOr<IReadOnlyList<PlanItem>> merged = split is null
                ? parsed
                : SplitMerge.Apply(split, parsed.Value);
            if (merged.IsError)
            {
                return $"被拒绝：{merged.FirstError.Description}";
            }

            List<string> branches = [];
            foreach (FlowEdge edge in task.Graph.Outgoing(run.Context.NodeIndex))
            {
                if (edge.Feed == EdgeFeed.Items && task.Graph[edge.To] is ExecutableNode { Branch: { } branch })
                {
                    branches.Add(branch);
                }
            }

            if (branches.Count > 0)
            {
                foreach (string? branch in merged.Value.Select(item => item.Branch))
                {
                    if (string.IsNullOrWhiteSpace(branch))
                    {
                        return "被拒绝：分流任务的条目必须写明分支 Branch。";
                    }

                    if (!branches.Contains(branch))
                    {
                        return $"被拒绝：分支 {branch} 没有对应的分支执行节点。";
                    }
                }
            }

            task.SetSplit(run.Context.NodeIndex, new PlanOutput(run.Id, merged.Value));

            return $"已记录 {merged.Value.Count} 个条目。";
        }
    }

    /// <summary>检查执行节点交回结论。按执行节点的实例记到执行状态的检查结论里。</summary>
    public string SubmitVerdict(TurnScope? scope, bool passed, string findings)
    {
        if (Owner(scope) is not { } run || run.Context.Output != NodeOutput.Review)
        {
            return "被拒绝：只有进行中的检查 run 能提交结论。";
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return "内部错误：该 run 没有归属任务。";
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            if (!passed && string.IsNullOrWhiteSpace(findings))
            {
                return "被拒绝：不通过时要列出问题。";
            }

            bool recorded = task.Runtime.Executable(run.Context.NodeIndex).RecordCheck(run.Context.ItemIndex,
                new CheckResult(run.Context.ExecutionCount, passed, findings, run.Id, run.Context.NodeName));

            if (!recorded)
            {
                return "本轮已提交过结论，无需重复提交。";
            }

            return passed ? "已记录：通过。" : "已记录：不通过。";
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

