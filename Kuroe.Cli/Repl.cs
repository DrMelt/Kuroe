using ErrorOr;
using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.Workflows;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Cli;

/// <summary>终端对话循环。</summary>
internal sealed class Repl(
    TaskRegistry registry,
    TaskService tasks,
    ReplCommands commands,
    DialogueSink sink,
    RunNotifier notifier,
    StartupView startup,
    Terminal terminal,
    ErrorPrinter errors)
{
    public async Task RunAsync()
    {
        using var cancellation = new TurnCancellation(registry, terminal);
        Terminal.OnInterrupt(cancellation.Cancel);

        notifier.Attach();
        startup.Print();

        while (true)
        {
            terminal.Append($"\n{Prompt}");
            string? input = Terminal.ReadLine()?.Trim();
            if (input is null)
            {
                break;
            }

            if (input.Length == 0)
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (input.StartsWith('/'))
            {
                commands.Execute(input);
                continue;
            }

            await AskAsync(input, cancellation);
        }

        await tasks.ShutdownAsync();
    }

    /// <summary>前台对话发生在当前任务上。</summary>
    private string Prompt => registry.Active is { } id ? $"{id} 用户 > " : "用户 > ";

    private async Task AskAsync(string input, TurnCancellation cancellation)
    {
        if (registry.Active is not { } id || registry.Find(id) is not { IsError: false } found)
        {
            terminal.Hint("还没有任务可对话，先 /task new <目标> 提交一个。");
            return;
        }

        AgentTask task = found.Value;
        ErrorOr<DialogueReply> reply;
        using (terminal.Exclusive())
        {
            terminal.Append("智能体 > ");
            try
            {
                reply = await task.AskDialogueAsync(input, sink, cancellation.Begin());
            }
            catch (OperationCanceledException)
            {
                terminal.NewLine();
                terminal.Line("已取消。");
                return;
            }
            catch (Exception ex)
            {
                terminal.NewLine();
                terminal.Error($"请求失败：{ex.Message}");
                return;
            }
        }

        if (reply.IsError)
        {
            errors.Report(reply.ErrorsOrEmptyList);
            commands.GuideCurrentModel();
            return;
        }

        if (!reply.Value.Text.EndsWith('\n'))
        {
            terminal.NewLine();
        }

        if (reply.Value.Discarded)
        {
            terminal.Hint("本轮内容未计入上下文。");
        }
    }
}

