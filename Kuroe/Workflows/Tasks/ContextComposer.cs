using Kuroe.Agent.Runs;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Agent.Turns;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>为叶子装配上下文。派出的 agent 只用这里给出的内容，装配规则集中在一处。
/// 收拢检查走跨单元的聚合装配，其余叶子共用单单元装配。</summary>
public static class ContextComposer
{
    /// <summary>单条种子文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>带进上下文的任务对话条数。</summary>
    private const int DialogueLimit = 6;

    /// <summary>契约工具名：按叶子产出契约自动附加到工具面。</summary>
    internal const string PlanToolName = "SubmitPlanItems";

    /// <summary>契约工具名：按叶子产出契约自动附加到工具面。</summary>
    internal const string ReviewToolName = "SubmitVerdict";

    /// <summary>按叶子声明为单元装配这一轮的上下文。规划、实施与逐条检查共用同一套装配。</summary>
    public static RunContext ForLeaf(AgentTask task, WorkUnit unit, LeafNode leaf, string model)
    {
        List<ContextMessage> seed = [];
        AppendDialogue(task, seed);
        foreach (int fromIndex in leaf.From)
        {
            AppendUpstream(task, unit, fromIndex, seed);
        }

        if (unit.Item is { } item)
        {
            string branch = unit.Branch is { } name ? $"\n实施分支：{name}" : string.Empty;
            seed.Add(new ContextMessage(MessageRole.User,
                Limit($"本条目：{item.Title}\n要做：{item.Instruction}\n验收标准：{item.Acceptance}{branch}"),
                new ItemSource(PlanOrigin(task, unit, leaf), item.Index, item.Title)));
        }

        AppendRework(unit, seed);
        int round = (unit.LastCheck?.Round ?? 0) + 1;

        return new RunContext
        {
            Task = task.Id,
            Output = leaf.Output,
            NodeIndex = leaf.Index,
            NodeName = leaf.Name,
            Instruction = Instruction(task, unit, leaf, round),
            Model = model,
            SystemPrompt = leaf.Agent.SystemPrompt,
            ItemIndex = unit.ItemIndex,
            Attempt = round,
            Tools = ToolsOf(leaf),
            Seed = seed,
        };
    }

    /// <summary>收拢检查的装配：跨全部条目单元汇总实施产出，整体交一个检查 agent，结论作用于全部。</summary>
    public static RunContext ForFunnel(AgentTask task, IReadOnlyList<WorkUnit> members, LeafNode leaf, string model, int round)
    {
        List<ContextMessage> seed = [];
        AppendDialogue(task, seed);
        foreach (int fromIndex in leaf.From)
        {
            if (task.Graph[fromIndex].Mode == NodeMode.PerItem)
            {
                AppendImplementations(task, members, fromIndex, seed);
            }
            else
            {
                AppendUpstream(task, members[0], fromIndex, seed);
            }
        }

        AppendFunnelRework(task, leaf, seed);
        List<string> lines = [];
        if (leaf.Prompt is { Length: > 0 } prompt)
        {
            lines.Add(prompt);
        }

        lines.Add($"目标：{task.Goal}");
        if (round > 1)
        {
            lines.Add($"这是第 {round} 轮整体检查。");
        }

        return new RunContext
        {
            Task = task.Id,
            Output = leaf.Output,
            NodeIndex = leaf.Index,
            NodeName = leaf.Name,
            Instruction = string.Join('\n', lines),
            Model = model,
            SystemPrompt = leaf.Agent.SystemPrompt,
            ItemIndex = null,
            Attempt = round,
            Tools = ToolsOf(leaf),
            Seed = seed,
        };
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

    /// <summary>当前叶子引用的拆分产出的出处 agent。从 From 里的规划产出找，找不到时按单元条目所属的拆分回退，仍找不到给空标识。</summary>
    private static RunId PlanOrigin(AgentTask task, WorkUnit unit, LeafNode leaf)
    {
        if (task.Graph.PlanSource(leaf.Index) is { } source && task.SplitFor(source) is { } plan)
        {
            return plan.Origin;
        }

        foreach (PlanOutput split in task.Splits.Values)
        {
            if (split.Items.Any(item => item.Index == unit.ItemIndex))
            {
                return split.Origin;
            }
        }

        return new RunId(0);
    }

    /// <summary>被引用叶子在本单元上的产出。</summary>
    private static void AppendUpstream(AgentTask task, WorkUnit unit, int fromIndex, List<ContextMessage> seed)
    {
        if (!unit.Nodes.TryGetValue(fromIndex, out AgentRun? run) || run.Result is not { Length: > 0 } result)
        {
            return;
        }

        seed.Add(new ContextMessage(MessageRole.User, Limit($"节点「{task.Graph[fromIndex].Name}」的产出：\n{result}"),
            new AgentSource(run.Id, task.Graph[fromIndex].Name)));
    }

    /// <summary>收拢检查引用的展开实施叶：逐条目列出实施产出，分支单元标注所属分支。</summary>
    private static void AppendImplementations(AgentTask task, IReadOnlyList<WorkUnit> members, int fromIndex, List<ContextMessage> seed)
    {
        string name = task.Graph[fromIndex].Name;
        foreach (WorkUnit member in members)
        {
            if (!member.Nodes.TryGetValue(fromIndex, out AgentRun? run) || run.Result is not { Length: > 0 } result)
            {
                continue;
            }

            string title = member.Item?.Title ?? $"条目 {member.ItemIndex + 1}";
            string branch = member.Branch is { } branchName ? $"{title}（{branchName}）" : title;
            seed.Add(new ContextMessage(MessageRole.User, Limit($"条目「{branch}」的实施产出：\n{result}"),
                new AgentSource(run.Id, name)));
        }
    }

    /// <summary>返工的实施上下文带上上一轮检查的意见与轮次。</summary>
    private static void AppendRework(WorkUnit unit, List<ContextMessage> seed)
    {
        if (unit.LastCheck is not { Passed: false } check)
        {
            return;
        }

        seed.Add(new ContextMessage(MessageRole.User,
            Limit($"上一轮检查未通过：\n{check.Findings}"), new AgentSource(check.Origin, check.NodeName)));
    }

    /// <summary>收拢检查的返工读任务级的整体结论。</summary>
    private static void AppendFunnelRework(AgentTask task, LeafNode leaf, List<ContextMessage> seed)
    {
        if (task.FunnelCheck(leaf.Name) is not { Passed: false } check)
        {
            return;
        }

        seed.Add(new ContextMessage(MessageRole.User,
            Limit($"上一轮整体检查未通过：\n{check.Findings}"), new AgentSource(check.Origin, check.NodeName)));
    }

    private static string Instruction(AgentTask task, WorkUnit unit, LeafNode leaf, int round)
    {
        List<string> lines = [];
        if (leaf.Prompt is { Length: > 0 } prompt)
        {
            lines.Add(prompt);
        }

        lines.Add(unit.Item is { } item
            ? $"目标：{task.Goal}\n本次只负责条目 {item.Index + 1}：{item.Title}{(unit.Branch is { } branch ? $"（分支 {branch}）" : string.Empty)}"
            : $"目标：{task.Goal}");

        AppendSplitGuide(task, leaf, lines);

        if (round > 1)
        {
            lines.Add($"这是第 {round} 轮实施，针对上一轮检查意见返工。");
        }

        return string.Join('\n', lines);
    }

    /// <summary>规划叶的拆分说明：固定条目作参考、补充上限与统一验收、并行段的分支清单。</summary>
    private static void AppendSplitGuide(AgentTask task, LeafNode leaf, List<string> lines)
    {
        if (leaf.Output != NodeOutput.Plan)
        {
            return;
        }

        if (leaf.Split is { } split)
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

        if (task.Graph.SegmentConsuming(leaf.Index) is { IsParallel: true } segment)
        {
            List<string> branches = [];
            foreach (int branch in segment.BranchLeaves)
            {
                branches.Add(task.Graph[branch].Name);
            }

            lines.Add($"可选分支：{string.Join('、', branches)}，条目须标明其一。");
        }
    }

    /// <summary>本轮工具面：agent 声明的能力工具加按产出契约附上的契约工具。</summary>
    private static List<string> ToolsOf(LeafNode leaf)
    {
        List<string> names = [.. leaf.Agent.Tools];
        if (leaf.Output == NodeOutput.Plan)
        {
            names.Add(PlanToolName);
        }

        if (leaf.Output == NodeOutput.Review)
        {
            names.Add(ReviewToolName);
        }

        return names;
    }

    private static string Limit(string text) =>
        text.Length <= TextLimit ? text : string.Concat(text.AsSpan(0, TextLimit), "…");
}