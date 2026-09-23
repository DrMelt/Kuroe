using ErrorOr;
using Kuroe.Agent;

namespace Kuroe.Workflows;

/// <summary>模型侧交回结构化产出的唯一入口：规划交条目拆分，检查交结论。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class UnitSubmitter(TaskRegistry registry)
{

    /// <summary>规划步骤交回条目拆分。</summary>
    public string SubmitPlan(TurnScope? scope, string itemsJson)
    {
        if (Owner(scope, RunRole.Plan) is not { } run)
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
            if (task.Plan?.Origin == run.Id)
            {
                return "本轮已提交过条目拆分，无需重复提交。";
            }

            task.Plan = new StepPlan(run.Id, parsed.Value);

            return $"已记录 {parsed.Value.Count} 个条目。";
        }
    }

    /// <summary>检查步骤交回结论。</summary>
    public string SubmitVerdict(TurnScope? scope, bool passed, string findings)
    {
        if (Owner(scope, RunRole.Check) is not { } run)
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
            if (task.UnitFor(run.Context.ItemIndex) is not { } unit)
            {
                return "内部错误：该 agent 没有对应的工作单元。";
            }

            if (unit.Verdict != UnitVerdict.NotChecked)
            {
                return "本条目已有结论，无需重复提交。";
            }

            if (!passed && string.IsNullOrWhiteSpace(findings))
            {
                return "被拒绝：不通过时要列出问题。";
            }

            unit.RecordVerdict(passed, findings);

            return passed ? "已记录：通过。" : "已记录：不通过。";
        }
    }

    /// <summary>提交者必须绑定了执行回合，且是该角色仍在跑的 agent。</summary>
    private AgentRun? Owner(TurnScope? scope, RunRole role) =>
        scope?.Run is { } id
        && registry.FindRun(id) is { IsError: false } found
        && found.Value.Context.Role == role
        && found.Value.IsLive
            ? found.Value
            : null;
}
