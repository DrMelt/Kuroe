using ErrorOr;
using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.Workflows.Dialogue;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Cli;

/// <summary>终端对话循环。提示符恒定在根层，普通输入全部交给常驻对话任务。</summary>
internal sealed class Repl(
    TaskService tasks,
    ReplCommands commands,
    DialogueSink sink,
    ExecutionNotifier notifier,
    StartupView startup,
    Terminal terminal,
    ErrorView errors,
    DialogueHost dialogue)
{
    public async Task RunAsync()
    {
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

            await AskAsync(input);
        }

        await tasks.ShutdownAsync();
    }

    /// <summary>提示符固定在根层，常驻对话任务随时可答。提示符来源行独立，输入从下一行开始。</summary>
    private static string Prompt => "Kuroe >";

    /// <summary>助手回合的框标题。</summary>
    private const string SourceLabel = "Kuroe 助手";

    private async Task AskAsync(string input)
    {
        ErrorOr<Success> outcome = Result.Success;
        string? failure = null;
        using (terminal.Exclusive())
        using (terminal.OpenFrame(BoxKind.Reply, SourceLabel))
        {
            try
            {
                outcome = await dialogue.ReplyAsync(input, sink);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }
        }

        if (failure is not null)
        {
            terminal.Error($"请求失败：{failure}");
            return;
        }

        if (outcome.IsError)
        {
            errors.Report(outcome.ErrorsOrEmptyList);
            commands.GuideCurrentModel();
        }
    }
}

