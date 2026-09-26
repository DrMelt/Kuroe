using ErrorOr;
using Kuroe.Agent.Runs;
using Kuroe.Agent.Turns;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>模型侧交回结构化产出的唯一入口：规划交条目拆分，检查交结论。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class UnitSubmitter(TaskRegistry registry)
{

    /// <summary>规划叶子交回条目拆分。并行段的分支拆分在这里校验，它交回的任务都要落在存在的分支上。</summary>
    public string SubmitPlan(TurnScope? scope, string itemsJson)
    {
        if (Owner(scope) is not { } run || run.Context.Output != NodeOutput.Plan)
        {
            return "被拒绝：只有进行中的规划 agent 能提交条目拆分。";
        }

        ErrorOr<AgentTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return "内部错误：该 agent 没有归属任务。";
        }

        AgentTask task = found.Value;
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

            SplitConfig? split = task.Graph[run.Context.NodeIndex].Split;
            ErrorOr<IReadOnlyList<PlanItem>> merged = split is null
                ? parsed
                : SplitMerge.Apply(split, parsed.Value);
            if (merged.IsError)
            {
                return $"被拒绝：{merged.FirstError.Description}";
            }

            if (task.Graph.SegmentConsuming(run.Context.NodeIndex) is { IsParallel: true } segment)
            {
                foreach (PlanItem item in merged.Value)
                {
                    if (string.IsNullOrWhiteSpace(item.Branch))
                    {
                        return "被拒绝：并行段的任务必须写明分支 Branch。";
                    }

                    if (task.Graph.BranchLeaf(segment, item.Branch) is null)
                    {
                        return $"被拒绝：分支 {item.Branch} 不在并行段里。";
                    }
                }
            }

            task.SetSplit(run.Context.NodeIndex, new PlanOutput(run.Id, merged.Value));

            return $"已记录 {merged.Value.Count} 个条目。";
        }
    }

    /// <summary>检查叶子交回结论。收拢检查记到任务上，逐条检查记到单元的最后一个检查活动。</summary>
    public string SubmitVerdict(TurnScope? scope, bool passed, string findings)
    {
        if (Owner(scope) is not { } run || run.Context.Output != NodeOutput.Review)
        {
            return "被拒绝：只有进行中的检查 agent 能提交结论。";
        }

        ErrorOr<AgentTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return "内部错误：该 agent 没有归属任务。";
        }

        AgentTask task = found.Value;
        lock (task.Gate)
        {
            if (run.Context.ItemIndex is null)
            {
                if (!passed && string.IsNullOrWhiteSpace(findings))
                {
                    return "被拒绝：不通过时要列出问题。";
                }

                bool recorded = task.RecordFunnelCheck(run, run.Context.NodeName, passed, findings);

                return recorded ? (passed ? "已记录：通过。" : "已记录：不通过。") : "本轮已提交过结论，无需重复提交。";
            }

            if (task.UnitFor(run.Context.ItemIndex) is not { } unit)
            {
                return "内部错误：该 agent 没有对应的工作单元。";
            }

            if (!passed && string.IsNullOrWhiteSpace(findings))
            {
                return "被拒绝：不通过时要列出问题。";
            }

            bool unitRecorded = unit.RecordCheck(run, passed, findings);

            return unitRecorded ? (passed ? "已记录：通过。" : "已记录：不通过。") : "本轮已提交过结论，无需重复提交。";
        }
    }

    /// <summary>提交者必须绑定了执行回合，且是该契约仍在跑的 agent。</summary>
    private AgentRun? Owner(TurnScope? scope) =>
        scope?.Run is { } id
        && registry.FindRun(id) is { IsError: false } found
        && found.Value.IsLive
            ? found.Value
            : null;
}
