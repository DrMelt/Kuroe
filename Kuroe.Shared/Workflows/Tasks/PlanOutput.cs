using Kuroe.Shared.Agent;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>规划或分配执行节点交回的一个条目，是按条目展开执行单元的依据。Branch 指定并行段内承担它的分支。</summary>
public sealed record PlanItem(int Index, string Title, string Instruction, string Acceptance, string? Branch);

/// <summary>规划执行节点的产出：由哪个 agent 交回的条目拆分。</summary>
public sealed record PlanOutput(RunId Origin, IReadOnlyList<PlanItem> Items);