using Kuroe.Agent;

namespace Kuroe.Workflows;

/// <summary>规划步骤的产出：由哪个 agent 交回的条目拆分。</summary>
public sealed record StepPlan(RunId Origin, IReadOnlyList<PlanItem> Items);

