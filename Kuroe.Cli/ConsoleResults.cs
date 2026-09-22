using ErrorOr;
using Kuroe.Agent;
using Kuroe.Configuration;

namespace Kuroe.Cli;

/// <summary>命令结果的终端呈现。</summary>
internal sealed class ConsoleResults(Terminal terminal, ConsoleErrors errors)
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

    /// <summary>执行设置改动，成功后按改动的影响给出提示。</summary>
    public void Apply(SettingsProvider settings, Func<ErrorOr<Success>> write)
    {
        AgentSettings before = settings.Current.Agent;

        ErrorOr<Success> result = write();
        if (result.IsError)
        {
            Reject(result.ErrorsOrEmptyList);
            return;
        }

        AgentSettings current = settings.Current.Agent;

        terminal.Ok(current.RequiresRestart(before) ? "已保存，重启后生效。" : "已保存并生效。");
        if (current.InvalidatesHistory(before))
        {
            terminal.Hint("下一轮对话将清空上下文。");
        }
    }

    /// <summary>报告错误并说明改动未生效，与错误同走 stderr。</summary>
    public void Reject(IEnumerable<Error> failures)
    {
        errors.Report(failures);
        terminal.Note("未生效。");
    }

    /// <summary>收集解析失败的错误。</summary>
    public static void Collect<T>(ErrorOr<T> parsed, List<Error> errors)
    {
        if (parsed.IsError)
        {
            errors.AddRange(parsed.ErrorsOrEmptyList);
        }
    }
}
