using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Agent.Turns;
using Kuroe.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>为步骤装配上下文。派生出的 agent 只用这里给出的内容，装配规则集中在一处。</summary>
public static class ContextComposer
{
    /// <summary>单条种子文本的上限。</summary>
    private const int TextLimit = 2000;

    /// <summary>带进上下文的任务对话条数。</summary>
    private const int DialogueLimit = 6;

    /// <summary>按步骤声明装配该单元这一轮的上下文。</summary>
    public static RunContext ForStep(AgentTask task, WorkUnit unit, int stepIndex, string model)
    {
        StepSpec spec = task.Flow.Steps[stepIndex];
        List<ContextMessage> seed = [];
        AppendDialogue(task, seed);
        foreach (string from in spec.From)
        {
            AppendUpstream(task, unit, from, seed);
        }

        if (unit.Item is { } item && task.Plan is { } plan)
        {
            seed.Add(new ContextMessage(MessageRole.User,
                Limit($"本条目：{item.Title}\n要做：{item.Instruction}\n验收标准：{item.Acceptance}"),
                new ItemSource(plan.Origin, item.Index, item.Title)));
        }

        AppendRework(task, unit, seed);

        return new RunContext
        {
            Task = task.Id,
            Role = spec.Role,
            StepIndex = stepIndex,
            StepName = spec.Name,
            Instruction = Instruction(task, unit, spec),
            Model = model,
            ItemIndex = unit.ItemIndex,
            Attempt = unit.Attempts,
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

    /// <summary>被引用步骤在本单元上的产出。</summary>
    private static void AppendUpstream(AgentTask task, WorkUnit unit, string from, List<ContextMessage> seed)
    {
        if (task.Flow.IndexOf(from) is not { } index
            || !unit.Steps.TryGetValue(index, out AgentRun? run)
            || run.Result is not { Length: > 0 } result)
        {
            return;
        }

        seed.Add(new ContextMessage(MessageRole.User, Limit($"步骤「{from}」的产出：\n{result}"), new AgentSource(run.Id, from)));
    }

    /// <summary>返工时带上一轮的检查意见。</summary>
    private static void AppendRework(AgentTask task, WorkUnit unit, List<ContextMessage> seed)
    {
        if (unit.Attempts <= 1 || unit.Findings is not { Length: > 0 } findings)
        {
            return;
        }

        foreach ((int index, AgentRun run) in unit.Steps.OrderByDescending(entry => entry.Key))
        {
            if (task.Flow.Steps[index].Role == RunRole.Check)
            {
                seed.Add(new ContextMessage(MessageRole.User, Limit($"上一轮检查未通过：\n{findings}"),
                    new AgentSource(run.Id, task.Flow.Steps[index].Name)));
                return;
            }
        }
    }

    private static string Instruction(AgentTask task, WorkUnit unit, StepSpec spec)
    {
        List<string> lines = [];
        if (spec.Prompt is { Length: > 0 } prompt)
        {
            lines.Add(prompt);
        }

        lines.Add(unit.Item is { } item
            ? $"目标：{task.Goal}\n本次只负责条目 {item.Index + 1}：{item.Title}"
            : $"目标：{task.Goal}");

        if (unit.Attempts > 1)
        {
            lines.Add($"这是第 {unit.Attempts} 轮实施，针对上一轮检查意见返工。");
        }

        return string.Join('\n', lines);
    }

    private static string Limit(string text) =>
        text.Length <= TextLimit ? text : string.Concat(text.AsSpan(0, TextLimit), "…");
}
