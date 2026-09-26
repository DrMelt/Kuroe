using Kuroe.Shared.Agent;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>规划叶子交回的一个条目，是按条目展开执行单元的依据。</summary>
public sealed record PlanItem(int Index, string Title, string Instruction, string Acceptance);

/// <summary>规划叶子的产出：由哪个 agent 交回的条目拆分。</summary>
public sealed record PlanOutput(RunId Origin, IReadOnlyList<PlanItem> Items);