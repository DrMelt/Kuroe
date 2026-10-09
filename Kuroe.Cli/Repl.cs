using ErrorOr;
using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.Shared.Workflows;
using Kuroe.Workflows;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Cli;

/// <summary>终端对话循环。提示符恒定在根层，普通输入全部交给常驻对话任务。</summary>
internal sealed class Repl(
    TaskRegistry registry,
    TaskService tasks,
    ReplCommands commands,
    DialogueSink sink,
    RunNotifier notifier,
    StartupView startup,
    Terminal terminal,
    ErrorView errors,
    DialogueHost dialogue)
{
    public async Task RunAsync()
    {
        using var cancellation = new TurnCancellation(registry, terminal);
        Terminal.OnInterrupt(cancellation.Cancel);

        notifier.Attach();
        startup.Print();

        while (true)
        {
            terminal.Append($"\n{Prompt}\n");
            string? input = Terminal.ReadLine()?.Trim();
            if (input is null)
            {
                break;
            }

            if (input.Length == 0)
            {
                continue;
            }

            if (input.StartsWith('/'))
            {
                if (commands.Execute(input))
                {
                    break;
                }

                continue;
            }

            await AskAsync(input, cancellation);
        }

        await tasks.ShutdownAsync();
    }

    /// <summary>提示符固定在根层，常驻对话任务随时可答。提示符来源行独立，输入从下一行开始。</summary>
    private static string Prompt => "root >";

    private async Task AskAsync(string input, TurnCancellation cancellation)
    {
        ErrorOr<DialogueReply> reply;
        using (terminal.Exclusive())
        {
            terminal.Line("对话 > ");
            sink.Reset();
            try
            {
                reply = await dialogue.ReplyAsync(input, sink, cancellation.Begin());
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
    }
}

