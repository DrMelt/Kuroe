using ErrorOr;
using Kuroe.Agent;
using Kuroe.Cli.Commands;

namespace Kuroe.Cli;

/// <summary>终端对话循环。</summary>
internal sealed class Repl(
    AgentSession session,
    ReplCommands commands,
    Terminal terminal,
    ConsoleErrors errors)
{
    private readonly AgentSession _session = session;
    private readonly ReplCommands _commands = commands;
    private readonly Terminal _terminal = terminal;
    private readonly ConsoleErrors _errors = errors;

    public async Task RunAsync()
    {
        using var cancellation = new TurnCancellation();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        _commands.PrintStartup();

        while (true)
        {
            _terminal.Append("\n用户 > ");
            string? input = Console.ReadLine()?.Trim();
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
                _commands.Execute(input);
                continue;
            }

            _terminal.Append("智能体 > ");
            try
            {
                ErrorOr<string> reply = await _session.AskAsync(input, _terminal.Append, cancellation.Begin());
                if (reply.IsError)
                {
                    _errors.Report(reply.ErrorsOrEmptyList);
                    _commands.GuideCurrentModel();
                }
                else if (!reply.Value.EndsWith('\n'))
                {
                    _terminal.NewLine();
                }
            }
            catch (OperationCanceledException)
            {
                _terminal.Line("已取消。");
            }
            catch (Exception ex)
            {
                _terminal.Error($"\n请求失败：{ex.Message}");
            }

            if (_session.LastTurnDiscarded)
            {
                _terminal.Hint("本轮内容未计入上下文。");
            }
        }
    }

    /// <summary>Ctrl+C 的取消范围，只作用于当前一轮请求。</summary>
    private sealed class TurnCancellation : IDisposable
    {
        private readonly Lock _gate = new();
        private CancellationTokenSource? _current;

        /// <summary>开始一轮请求并返回该轮的取消令牌，上一轮的取消源随之释放。</summary>
        public CancellationToken Begin()
        {
            lock (_gate)
            {
                _current?.Dispose();
                _current = new CancellationTokenSource();

                return _current.Token;
            }
        }

        /// <summary>取消当前一轮请求，没有进行中的请求时无事发生。</summary>
        public void Cancel()
        {
            lock (_gate)
            {
                _current?.Cancel();
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _current?.Dispose();
                _current = null;
            }
        }
    }
}
