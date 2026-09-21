using ApiHub.Models;
using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Cli;

/// <summary>终端对话循环。</summary>
public static class Repl
{
    public static async Task RunAsync(AgentSession session, SettingsProvider settings, CatalogService catalog)
    {
        using var cancellation = new TurnCancellation();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        CatalogContents contents = catalog.Snapshot();
        Console.WriteLine($"Kuroe 已启动，当前模型 {settings.Current.Agent.Model.Value}，" +
            $"目录中有 {contents.Providers.Length} 个提供商、{contents.Models.Length} 个模型。");
        if (contents.Providers.Length == 0)
        {
            Console.WriteLine("先 /provider add <提供商> <端点> <凭据> 添加提供商，再 /model add <模型> <提供商> 注册模型。");
        }

        Console.WriteLine("输入 exit 退出，/help 查看命令，Ctrl+C 中断当前回复。");

        while (true)
        {
            Console.Write("\n用户 > ");
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
                ReplCommands.Execute(input, session, settings, catalog);
                continue;
            }

            Console.Write("智能体 > ");
            try
            {
                ErrorOr<string> reply = await session.AskAsync(input, Console.Write, cancellation.Begin());
                if (reply.IsError)
                {
                    Errors.Report(reply.ErrorsOrEmptyList);
                    Errors.GuideModelRegistration(catalog, settings.Current.Agent.Model);
                }
                else if (!reply.Value.EndsWith('\n'))
                {
                    Console.WriteLine();
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("已取消。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\n请求失败：{ex.Message}");
            }

            if (session.LastTurnDiscarded)
            {
                Console.WriteLine("本轮内容未计入上下文。");
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
