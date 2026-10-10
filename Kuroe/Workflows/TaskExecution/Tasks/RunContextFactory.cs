using ApiHub.Shared.Models;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.TaskExecution.Tasks;

/// <summary>为执行节点装配上下文。启动的执行只用这里给出的内容，装配规则集中在一处。
/// 每条依赖边按它的消费方式取值：单份产出、拆分条目、全部实例或按条目对齐。</summary>
internal static class RunContextFactory
{
    /// <summary>单条种子文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>为执行节点的实例装配这一轮的上下文。itemIndex 为空表示整节点实例。</summary>
    internal static RunContext Create(WorkTask task, RuntimeExecutable node, int? itemIndex, ModelName model)
    {
        List<ContextMessage> seed = [];
        List<ContextMessage> contextInput = [];
        foreach (FlowEdge edge in task.Graph.Incoming(node.Index))
        {
            if (edge.Role == EdgeRole.Trigger)
            {
                // 触发输入端口不取数，来源产出不进上下文，只作启动信号
                continue;
            }

            List<ContextMessage> target = edge.Role == EdgeRole.ContextInput ? contextInput : seed;
            AppendUpstreamOutput(task, edge, itemIndex, target);
        }

        // 上下文输入端口的内容与系统指令一起构成请求前缀段，置于种子最前
        seed.InsertRange(0, contextInput);

        PlanItem? item = ItemOf(task, node, itemIndex);
        if (item is not null)
        {
            string branch = item.Branch is { } name ? $"\n实施分支：{name}" : string.Empty;
            seed.Add(new ContextMessage(MessageRole.User,
                Truncate($"本条目：{item.Title}\n要做：{item.Instruction}\n验收标准：{item.Acceptance}{branch}"),
                new ItemSource(OriginOf(task, node), item.Index, item.Title)));
        }

        int count = node.ExecutionCount(itemIndex) + 1;

        return new RunContext
        {
            Task = task.Id,
            Output = node.Output,
            NodeIndex = node.Index,
            NodeName = node.Name,
            Instruction = BuildInstruction(task, node, item, count),
            Model = model,
            ItemIndex = itemIndex,
            ExecutionCount = count,
            Tools = ToolsFor(node.Executable),
            Seed = seed,
            SystemPrompt = node.Executable.SystemPrompt.Count == 0 ? null : string.Join('\n', node.Executable.SystemPrompt),
            OutputPorts = node.Executable.Outputs,
        };
    }

    /// <summary>按一条依赖边的消费方式追加上游产出：对齐边按目标实例取来源实例的整份，其余按边上的端口取来源产出消息集。</summary>
    private static void AppendUpstreamOutput(WorkTask task, FlowEdge edge, int? itemIndex, List<ContextMessage> seed)
    {
        if (edge.Feed == EdgeFeed.Aligned)
        {
            if (itemIndex is { } index)
            {
                AppendPortMessages(task, edge.From, edge.Port, index, seed);
            }

            return;
        }

        AppendPortMessages(task, edge.From, edge.Port, null, seed);
    }

    /// <summary>取来源节点某端口的注入消息集并入种子：整份直接取放行产出，命名端口与上下文端口返回节点已构造的消息。</summary>
    private static void AppendPortMessages(WorkTask task, int fromIndex, PortName port, int? item, List<ContextMessage> seed)
    {
        foreach (ContextMessage message in task.Runtime[fromIndex].OutputMessages(task, port, item))
        {
            seed.Add(new ContextMessage(message.Role, Truncate(message.Text), message.Source));
        }
    }
    /// <summary>来源节点上某实例最近一次成功收口且产出非空的 run。</summary>
    private static Run? LatestSucceededRun(WorkTask task, int node, int? item) =>
        task.Runs.LastOrDefault(run =>
            run.Context.NodeIndex == node
            && run.Context.ItemIndex == item
            && run.State == RunState.Succeeded
            && run.Result is { Length: > 0 });

    /// <summary>按条目展开时本实例的条目内容，整节点实例为空。条目身份只在归属空间内有效。</summary>
    private static PlanItem? ItemOf(WorkTask task, RuntimeExecutable node, int? itemIndex)
    {
        if (itemIndex is not { } index || node.Mode != NodeMode.PerItem)
        {
            return null;
        }

        return task.Graph.ItemSpace(node.Index) is { } space && task.SplitFor(space) is { } split
            ? split.Items.FirstOrDefault(item => item.Index == index)
            : null;
    }

    /// <summary>条目内容的出处 run：优先用拆分来源的规划 run，找不到按条目归属空间取拆分的交回者。</summary>
    private static RunId OriginOf(WorkTask task, RuntimeExecutable node)
    {
        if (task.Graph.ItemSource(node.Index) is { } plan
            && LatestSucceededRun(task, plan, null) is { } planRun)
        {
            return planRun.Id;
        }

        if (task.Graph.ItemSpace(node.Index) is { } space && task.SplitFor(space) is { } split)
        {
            return split.Origin;
        }

        return new RunId(0);
    }

    /// <summary>指令正文：节点要求、目标与第几轮。</summary>
    private static string BuildInstruction(WorkTask task, RuntimeExecutable node, PlanItem? item, int count)
    {
        List<string> lines = [];
        if (node.Executable.Execution.Prompt is { Length: > 0 } prompt)
        {
            lines.Add(prompt);
        }

        if (item is { } entry)
        {
            string branch = entry.Branch is { } name ? $"（分支 {name}）" : string.Empty;
            lines.Add($"目标：{task.Goal}\n本次只负责条目 {entry.Index + 1}：{entry.Title}{branch}");
        }
        else
        {
            lines.Add($"目标：{task.Goal}");
        }

        AppendSplitGuide(task, node, lines);

        if (count > 1)
        {
            lines.Add($"这是第 {count} 轮实施。");
        }

        return string.Join('\n', lines);
    }

    /// <summary>规划执行节点的拆分说明：固定条目作参考、补充上限与统一验收、可选分支清单。</summary>
    private static void AppendSplitGuide(WorkTask task, RuntimeExecutable node, List<string> lines)
    {
        if (node.Output != NodeOutput.Plan)
        {
            return;
        }

        if (node.Executable.Execution.Split is { } split)
        {
            if (split.Items is { Count: > 0 } items)
            {
                List<string> listed = [];
                int number = 1;
                foreach (SplitItem item in items)
                {
                    string acceptance = item.Acceptance.Length > 0 ? $"｜验收：{item.Acceptance}" : string.Empty;
                    string branch = item.Branch is { } name ? $"｜分支：{name}" : string.Empty;
                    listed.Add($"{number}. {item.Title}｜{item.Instruction}{acceptance}{branch}");
                    number++;
                }

                lines.Add($"已按标准固定 {items.Count} 条：\n{string.Join('\n', listed)}");
            }

            if (split.ExtrasMax is { } limit)
            {
                lines.Add(split.Acceptance is { Length: > 0 } acceptance
                    ? $"请补充至多 {limit} 条新条目，每条给出标题与做法，不要重述已列条目；验收统一为：{acceptance}"
                    : $"请补充至多 {limit} 条新条目，每条给出标题、做法与验收标准，不要重述已列条目。");
            }
        }

        List<string> branches = [];
        foreach (FlowEdge edge in task.Graph.Outgoing(node.Index))
        {
            if (edge.Feed == EdgeFeed.Items && task.Graph[edge.To] is ExecutableNode { Execution.Branch: { } name })
            {
                branches.Add(name.Value);
            }
        }

        if (branches.Count > 0)
        {
            lines.Add($"可选分支：{string.Join('、', branches)}，条目须标明其一。");
        }
    }

    /// <summary>本轮工具面：节点声明的能力工具加按产出契约附上的契约工具。</summary>
    private static List<ToolPath> ToolsFor(ExecutableNode node)
    {
        List<ToolPath> names = [.. node.Execution.Tools];
        if (node.Execution.Output == NodeOutput.Plan)
        {
            names.Add(ToolPath.ContractPlan);
        }

        if (node.HasOutputPorts)
        {
            names.Add(ToolPath.ContractPortValues);
        }

        return names;
    }

    private static string Truncate(string text) =>
        text.Length <= TextLimit ? text : string.Concat(text.AsSpan(0, TextLimit), "…");
}
