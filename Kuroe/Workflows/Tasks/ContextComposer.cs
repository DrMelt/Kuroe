using Kuroe.Agent.Runs;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>为执行节点装配上下文。派出的 agent 只用这里给出的内容，装配规则集中在一处。
/// 每条依赖边按它的消费方式取值：单份产出、拆分条目、全部实例或按条目对齐。</summary>
public static class ContextComposer
{
    /// <summary>单条种子文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>带进上下文的任务对话条数。</summary>
    private const int DialogueLimit = 6;

    /// <summary>契约工具名：按执行节点产出契约自动附加到工具面。</summary>
    internal const string PlanToolName = "SubmitPlanItems";

    /// <summary>契约工具名：按执行节点产出契约自动附加到工具面。</summary>
    internal const string ReviewToolName = "SubmitVerdict";

    /// <summary>为执行节点的实例装配这一轮的上下文。itemIndex 为空表示整节点实例。</summary>
    public static RunContext ForExecutable(AgentTask task, ExecutableNode executable, int? itemIndex, string model)
    {
        List<ContextMessage> seed = [];
        AppendDialogue(task, seed);
        foreach (FlowEdge edge in task.Graph.Incoming(executable.Index))
        {
            AppendSource(task, edge, itemIndex, seed);
        }

        PlanItem? item = ItemOf(task, executable, itemIndex);
        if (item is not null)
        {
            string branch = item.Branch is { Length: > 0 } name ? $"\n实施分支：{name}" : string.Empty;
            seed.Add(new ContextMessage(MessageRole.User,
                Limit($"本条目：{item.Title}\n要做：{item.Instruction}\n验收标准：{item.Acceptance}{branch}"),
                new ItemSource(OriginOf(task, executable), item.Index, item.Title)));
        }

        CheckResult? rework = ReworkOf(task, executable, itemIndex);
        if (rework is not null)
        {
            seed.Add(new ContextMessage(MessageRole.User,
                Limit($"上一轮检查未通过：\n{rework.Findings}"), new AgentSource(rework.Origin, rework.NodeName)));
        }

        int count = task.Runtime.ExecutionCount(executable.Index, itemIndex);

        return new RunContext
        {
            Task = task.Id,
            Output = executable.Output,
            NodeIndex = executable.Index,
            NodeName = executable.Name,
            Instruction = Instruction(task, executable, item, count, rework is not null),
            Model = model,
            SystemPrompt = executable.Agent.SystemPrompt,
            ItemIndex = itemIndex,
            ExecutionCount = count,
            Tools = ToolsOf(executable),
            Seed = seed,
        };
    }

    /// <summary>按一条依赖边的消费方式追加上游产出。</summary>
    private static void AppendSource(AgentTask task, FlowEdge edge, int? itemIndex, List<ContextMessage> seed)
    {
        switch (edge.Feed)
        {
            case EdgeFeed.Items:
            case EdgeFeed.Single:
                AppendLatestRun(task, edge.From, null, seed);
                break;

            case EdgeFeed.AllInstances:
                if (task.Runtime.ExpandedItems(edge.From) is { } items)
                {
                    foreach (int item in items)
                    {
                        AppendLatestRun(task, edge.From, item, seed);
                    }
                }

                break;

            case EdgeFeed.Aligned:
                if (itemIndex is { } index)
                {
                    AppendLatestRun(task, edge.From, index, seed);
                }

                break;
        }
    }

    /// <summary>来源节点上某实例最近一次成功收口且产出非空的 agent。</summary>
    private static AgentRun? LatestSucceeded(AgentTask task, int node, int? item) =>
        task.Runs.LastOrDefault(run =>
            run.Context.NodeIndex == node
            && run.Context.ItemIndex == item
            && run.State == RunState.Succeeded
            && run.Result is { Length: > 0 });

    /// <summary>整节点或实例产出作为一条上下文，出处标注到该 agent。</summary>
    private static void AppendLatestRun(AgentTask task, int fromIndex, int? item, List<ContextMessage> seed)
    {
        if (LatestSucceeded(task, fromIndex, item) is not { } run)
        {
            return;
        }

        string name = task.Graph[fromIndex].Name;
        string prefix = item is { } index
            ? $"条目「{ItemTitle(task, fromIndex, index)}」在节点「{name}」的产出：\n"
            : $"节点「{name}」的产出：\n";
        seed.Add(new ContextMessage(MessageRole.User, Limit($"{prefix}{run.Result}"),
            new AgentSource(run.Id, name)));
    }

    /// <summary>任务已有的对话只带最近几条，更早的内容由上游产出概括。</summary>
    private static void AppendDialogue(AgentTask task, List<ContextMessage> seed)
    {
        List<ContextMessage> history = [];
        int turn = 0;
        foreach (JournalEntry entry in task.Journal.Entries)
        {
            switch (entry)
            {
                case PromptEntry prompt:
                    history.Add(new ContextMessage(MessageRole.User, Limit(prompt.Text), new DialogueSource(task.Id, ++turn)));
                    break;

                case TextEntry text when turn > 0:
                    history.Add(new ContextMessage(MessageRole.Assistant, Limit(text.Text), new DialogueSource(task.Id, turn)));
                    break;
            }
        }

        seed.AddRange(history.Skip(Math.Max(0, history.Count - DialogueLimit)));
    }

    /// <summary>按条目展开时本实例的条目内容，整节点实例为空。条目身份只在归属空间内有效。</summary>
    private static PlanItem? ItemOf(AgentTask task, ExecutableNode executable, int? itemIndex)
    {
        if (itemIndex is not { } index || executable.Mode != NodeMode.PerItem)
        {
            return null;
        }

        return task.Graph.ItemSpace(executable.Index) is { } space && task.SplitFor(space) is { } split
            ? split.Items.FirstOrDefault(item => item.Index == index)
            : null;
    }

    /// <summary>条目内容的出处 agent：优先用拆分来源的规划 run，找不到按条目归属空间取拆分的交回者。</summary>
    private static RunId OriginOf(AgentTask task, ExecutableNode executable)
    {
        if (task.Graph.ItemSource(executable.Index) is { } plan
            && LatestSucceeded(task, plan, null) is { } planRun)
        {
            return planRun.Id;
        }

        if (task.Graph.ItemSpace(executable.Index) is { } space && task.SplitFor(space) is { } split)
        {
            return split.Origin;
        }

        return new RunId(0);
    }

    /// <summary>条目的标题，取不到时退回序号。条目身份只在归属空间内有效。</summary>
    private static string ItemTitle(AgentTask task, int fromIndex, int itemIndex) =>
        task.Graph.ItemSpace(fromIndex) is { } space && task.SplitFor(space) is { } split
            ? split.Items.FirstOrDefault(item => item.Index == itemIndex)?.Title ?? $"条目 {itemIndex + 1}"
            : $"条目 {itemIndex + 1}";

    /// <summary>上一轮被拒的检查结论：检查执行节点读自己，实施执行节点读引用它的检查执行节点。</summary>
    private static CheckResult? ReworkOf(AgentTask task, ExecutableNode executable, int? itemIndex) =>
        task.Runtime.ReworkFor(executable.Index, itemIndex);

    /// <summary>指令正文：节点要求、目标与第几轮。</summary>
    private static string Instruction(AgentTask task, ExecutableNode executable, PlanItem? item, int count, bool reworked)
    {
        List<string> lines = [];
        if (executable.Prompt is { Length: > 0 } prompt)
        {
            lines.Add(prompt);
        }

        if (item is { } entry)
        {
            string branch = entry.Branch is { Length: > 0 } name ? $"（分支 {name}）" : string.Empty;
            lines.Add($"目标：{task.Goal}\n本次只负责条目 {entry.Index + 1}：{entry.Title}{branch}");
        }
        else
        {
            lines.Add($"目标：{task.Goal}");
        }

        AppendSplitGuide(task, executable, lines);

        if (count > 1)
        {
            string scope = executable.Output == NodeOutput.Review ? "检查" : "实施";
            lines.Add(reworked
                ? $"这是第 {count} 轮{scope}，针对上一轮检查意见返工。"
                : $"这是第 {count} 轮{scope}。");
        }

        return string.Join('\n', lines);
    }

    /// <summary>规划执行节点的拆分说明：固定条目作参考、补充上限与统一验收、可选分支清单。</summary>
    private static void AppendSplitGuide(AgentTask task, ExecutableNode executable, List<string> lines)
    {
        if (executable.Output != NodeOutput.Plan)
        {
            return;
        }

        if (executable.Split is { } split)
        {
            if (split.Items is { Count: > 0 } items)
            {
                List<string> listed = [];
                int number = 1;
                foreach (SplitItem item in items)
                {
                    string acceptance = item.Acceptance.Length > 0 ? $"｜验收：{item.Acceptance}" : string.Empty;
                    string branch = item.Branch is { Length: > 0 } name ? $"｜分支：{name}" : string.Empty;
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
        foreach (FlowEdge edge in task.Graph.Outgoing(executable.Index))
        {
            if (edge.Feed == EdgeFeed.Items && task.Graph[edge.To].Branch is { } name)
            {
                branches.Add(name);
            }
        }

        if (branches.Count > 0)
        {
            lines.Add($"可选分支：{string.Join('、', branches)}，条目须标明其一。");
        }
    }

    /// <summary>本轮工具面：agent 声明的能力工具加按产出契约附上的契约工具。</summary>
    private static List<string> ToolsOf(ExecutableNode executable)
    {
        List<string> names = [.. executable.Agent.Tools];
        if (executable.Output == NodeOutput.Plan)
        {
            names.Add(PlanToolName);
        }

        if (executable.Output == NodeOutput.Review)
        {
            names.Add(ReviewToolName);
        }

        return names;
    }

    private static string Limit(string text) =>
        text.Length <= TextLimit ? text : string.Concat(text.AsSpan(0, TextLimit), "…");
}