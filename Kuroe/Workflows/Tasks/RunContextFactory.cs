using ApiHub.Shared.Models;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>为执行节点装配上下文。启动的执行只用这里给出的内容，装配规则集中在一处。
/// 每条依赖边按它的消费方式取值：单份产出、拆分条目、全部实例或按条目对齐。</summary>
internal static class RunContextFactory
{
    /// <summary>单条种子文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>带进上下文的任务对话条数。</summary>
    private const int DialogueLimit = 6;

    /// <summary>为执行节点的实例装配这一轮的上下文。itemIndex 为空表示整节点实例。</summary>
    internal static RunContext Create(WorkTask task, RuntimeExecutable node, int? itemIndex, ModelName model)
    {
        List<ContextMessage> seed = [];
        List<ContextMessage> contextInput = [];
        foreach (FlowEdge edge in task.Graph.Incoming(node.Index))
        {
            List<ContextMessage> target = edge.IsContextInput ? contextInput : seed;
            AppendUpstreamOutput(task, edge, itemIndex, target);
        }

        // 上下文输入端口的内容置于对话之前，与 SystemPrompt 一起构成稳定的请求前缀段
        AppendDialogue(task, seed);
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

    /// <summary>按一条依赖边的消费方式追加上游产出。整份、整集与容器来源走同一个放行产出取数。</summary>
    private static void AppendUpstreamOutput(WorkTask task, FlowEdge edge, int? itemIndex, List<ContextMessage> seed)
    {
        if (edge.Feed == EdgeFeed.Aligned)
        {
            if (itemIndex is { } index)
            {
                AppendLatestRun(task, edge.From, index, port: null, seed);
            }

            return;
        }

        foreach ((int source, int? item) in task.Runtime[edge.From].ReleasedOutputs())
        {
            AppendLatestRun(task, source, item, edge.FromPort, seed);
        }
    }
    /// <summary>来源节点上某实例最近一次成功收口且产出非空的 run。</summary>
    private static Run? LatestSucceededRun(WorkTask task, int node, int? item) =>
        task.Runs.LastOrDefault(run =>
            run.Context.NodeIndex == node
            && run.Context.ItemIndex == item
            && run.State == RunState.Succeeded
            && run.Result is { Length: > 0 });

    /// <summary>整节点或实例产出作为一条上下文，出处标注到该 run。带端口时取该轮交回的命名段。</summary>
    private static void AppendLatestRun(WorkTask task, int fromIndex, int? item, PortName? port, List<ContextMessage> seed)
    {
        NodeName name = task.Graph[fromIndex].Name;

        // 输入节点的产出是用户回答，不落在 run 上
        if (task.Graph[fromIndex] is ExecutableNode { Execution.Output: NodeOutput.Input })
        {
            if (task.Runtime.Executable(fromIndex).InputAnswer is { Length: > 0 } answer)
            {
                seed.Add(new ContextMessage(MessageRole.User, Truncate($"节点「{name}」的输入：\n{answer}"),
                    new InputSource(name)));
            }

            return;
        }

        if (LatestSucceededRun(task, fromIndex, item) is not { } run)
        {
            return;
        }

        // 隐式上下文端口：上游 run 的装配上下文统一结构整体注入，每条消息的角色与出处原样保留，指令单列
        if (port == ExecutableNode.ContextOutputPort)
        {
            ContextFrame frame = new(run.Id, name, item, run.Context.Seed, run.Context.Instruction);

            foreach (ContextMessage message in frame.Messages)
            {
                seed.Add(new ContextMessage(message.Role, Truncate(message.Text), message.Source));
            }

            seed.Add(new ContextMessage(MessageRole.User,
                Truncate($"节点「{frame.Node}」的指令：\n{frame.Instruction}"),
                new ContextFrameSource(frame.Run, frame.Node, frame.Item)));
            return;
        }

        // 端口引用只取该轮交回的命名段：未交回不属于可注入的产出，不回退到整份文本
        if (port is { } outputPort)
        {
            if (task.PortValuesFor(fromIndex)?.GetValueOrDefault(outputPort) is { Length: > 0 } portContent)
            {
                seed.Add(new ContextMessage(MessageRole.User,
                    Truncate($"节点「{name}」的端口「{outputPort}」产出：\n{portContent}"),
                    new RunSource(run.Id, name)));
            }

            return;
        }

        string content = run.Result ?? string.Empty;
        string prefix = item is { } index
            ? $"条目「{ItemTitle(task, fromIndex, index)}」在节点「{name}」的产出：\n"
            : $"节点「{name}」的产出：\n";
        seed.Add(new ContextMessage(MessageRole.User, Truncate($"{prefix}{content}"),
            new RunSource(run.Id, name)));
    }

    /// <summary>任务已有的对话只带最近几条，更早的内容由上游产出概括。</summary>
    private static void AppendDialogue(WorkTask task, List<ContextMessage> seed)
    {
        List<ContextMessage> history = [];
        int turn = 0;
        foreach (JournalEntry entry in task.Journal.Entries)
        {
            switch (entry)
            {
                case PromptEntry prompt:
                    history.Add(new ContextMessage(MessageRole.User, Truncate(prompt.Text), new DialogueSource(task.Id, ++turn)));
                    break;

                case TextEntry text when turn > 0:
                    history.Add(new ContextMessage(MessageRole.Assistant, Truncate(text.Text), new DialogueSource(task.Id, turn)));
                    break;
            }
        }

        seed.AddRange(history.Skip(Math.Max(0, history.Count - DialogueLimit)));
    }

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

    /// <summary>条目的标题，取不到时退回序号。条目身份只在归属空间内有效。</summary>
    private static string ItemTitle(WorkTask task, int fromIndex, int itemIndex) =>
        task.Graph.ItemSpace(fromIndex) is { } space && task.SplitFor(space) is { } split
            ? split.Items.FirstOrDefault(item => item.Index == itemIndex)?.Title ?? $"条目 {itemIndex + 1}"
            : $"条目 {itemIndex + 1}";

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
