using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>内置流程：用户层未指定默认流程时使用，工作目录里还没有 flows.json 时也只有这一条。</summary>
internal static class DefaultFlows
{
    /// <summary>内置流程的名字。</summary>
    internal const string Name = "默认";

    /// <summary>内置流程：规划交回条目，逐条实施，逐条检查，检查不通过退回返工。</summary>
    internal static readonly Workflow Builtin = new(Name, "内置流程：规划、实施、检查",
    [
        new StepSpec
        {
            Name = "规划",
            Role = RunRole.Plan,
            Prompt = "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。",
        },
        new StepSpec
        {
            Name = "实施",
            Role = RunRole.Implement,
            Scope = StepScope.PerItem,
            From = ["规划"],
        },
        new StepSpec
        {
            Name = "检查",
            Role = RunRole.Check,
            Scope = StepScope.PerItem,
            From = ["规划", "实施"],
            OnReject = RejectAction.Retry,
        },
    ]);
}