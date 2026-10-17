using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>规划或分配执行节点交回的一个条目，是按条目展开执行单元的依据。Branch 指定并行段内承担它的分支。</summary>
public sealed record WorkItem(int Index, string Title, string Instruction, string Acceptance, BranchName? Branch);

/// <summary>声明 WorkItems 端口的执行节点的产出：由哪个 run 交回的条目拆分。</summary>
public sealed record WorkItemList(RunId Origin, IReadOnlyList<WorkItem> Items);
