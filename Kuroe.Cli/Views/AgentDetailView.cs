using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>agent 详情：头部、装配进来的上下文及其出处、逐步过程。</summary>
internal sealed class AgentDetailView(Terminal terminal)
{
    /// <summary>上下文一行能看到的文本长度。</summary>
    private const int SeedLimit = 70;

    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task, RunSnapshot run)
    {
        RunContext context = run.Context;
        _terminal.Line($"{run.Id} · {context.Role.Label()} · {context.StepName} · {Labels.Item(context.ItemIndex)} · 第 {context.Attempt} 轮");
        _terminal.Line($"{task.Id} {task.Title}　步骤 {context.StepIndex + 1}/{task.TotalSteps}　模型 {context.Model}");
        _terminal.Line($"状态 {Labels.State(run)}　{Labels.Clock(run.StartedAt)} → {Labels.Clock(run.FinishedAt)}　耗时 {Labels.Elapsed(run.Elapsed)}");

        foreach (string failure in run.Failures)
        {
            _terminal.Failure($"失败：{failure}");
        }

        _terminal.NewLine();
        _terminal.Line("指令：");
        _terminal.Line(context.Instruction);

        _terminal.NewLine();
        _terminal.Line($"上下文来源（{context.Seed.Count} 条）：");
        if (context.Seed.Count == 0)
        {
            _terminal.Hint("  没有装配任何已有内容。");
        }

        foreach (ContextMessage message in context.Seed)
        {
            _terminal.Line($"  {message.Source.Label}　{OneLine(message.Text)}");
        }

        _terminal.NewLine();
        _terminal.Line("过程：");
        JournalPrinter.Print(_terminal, run.Journal, run.DroppedEntries);

        if (run.Result is { Length: > 0 } result)
        {
            _terminal.NewLine();
            _terminal.Line("结论：");
            _terminal.Line(result);
        }
    }

    private static string OneLine(string text)
    {
        string flat = text.Replace('\n', ' ').Replace('\r', ' ');

        return flat.Length <= SeedLimit ? flat : string.Concat(flat.AsSpan(0, SeedLimit), "…");
    }
}
