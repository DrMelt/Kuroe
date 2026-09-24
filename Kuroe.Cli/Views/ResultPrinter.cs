using ErrorOr;
using Kuroe.Shared.Configuration;

namespace Kuroe.Cli.Views;

/// <summary>命令结果的终端呈现。</summary>
internal sealed class ResultPrinter(Terminal terminal, ErrorPrinter errors)
{
    /// <summary>成功时打印完成提示，失败时打印错误与未生效提示。</summary>
    public void Report(ErrorOr<Success> result, string done)
    {
        if (result.IsError)
        {
            Reject(result.ErrorsOrEmptyList);
            return;
        }

        terminal.Ok(done);
    }

    /// <summary>呈现一次设置改动：成功按影响给出提示，失败说明未生效。影响由库判定。</summary>
    public void Apply(ErrorOr<SettingsEffect> result)
    {
        if (result.IsError)
        {
            Reject(result.ErrorsOrEmptyList);
            return;
        }

        SettingsEffect effect = result.Value;

        terminal.Ok(effect.RequiresRestart ? "已保存，重启后生效。" : "已保存并生效。");
        if (effect.InvalidatesHistory)
        {
            terminal.Hint("下一轮对话将清空上下文。");
        }
    }

    /// <summary>报告错误、给出可操作提示，并说明改动未生效，与错误同走 stderr。</summary>
    public void Reject(IEnumerable<Error> failures)
    {
        Error[] reported = [.. failures];

        errors.Report(reported);
        errors.Guide(reported);
        terminal.Note("未生效。");
    }
}
