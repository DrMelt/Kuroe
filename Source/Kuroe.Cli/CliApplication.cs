using ErrorOr;
using Kuroe.Cli.Commands;
using Kuroe.Shared;

namespace Kuroe.Cli;

/// <summary>命令执行的边界：装配一次会话、执行一个操作、释放会话并给出退出码。</summary>
internal sealed class CliApplication(
    TextWriter output,
    TextWriter error,
    Func<string?, ErrorOr<CliSession>> sessionFactory)
{
    private readonly CliOutput _output = new(output, error);

    /// <summary>提交任务并在本进程内驱动。</summary>
    public Task<int> RunAsync(
        string? directory,
        string flow,
        string? title,
        string[] goal) =>
        ExecuteAsync(directory, (session, commandOutput) =>
            new RunCommand(session, commandOutput)
                .RunAsync(flow, title, Join(goal)));

    /// <summary>执行一个同步命令。</summary>
    public Task<int> ExecuteAsync(string? directory, Func<CliSession, CliOutput, int> action) =>
        WithSessionAsync(directory, session => Task.FromResult(action(session, _output)));

    /// <summary>执行一个异步命令。</summary>
    public Task<int> ExecuteAsync(string? directory, Func<CliSession, CliOutput, Task<int>> action) =>
        WithSessionAsync(directory, session => action(session, _output));

    private async Task<int> WithSessionAsync(string? directory, Func<CliSession, Task<int>> action)
    {
        ErrorOr<CliSession> created;
        try
        {
            created = sessionFactory(directory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportInternal(ex);

            return ExitCodes.Failure;
        }

        if (created.IsError)
        {
            _output.Report(created.ErrorsOrEmptyList);

            return ExitCodes.Failure;
        }

        try
        {
            await using CliSession session = created.Value;

            return await action(session).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportInternal(ex);

            return ExitCodes.Failure;
        }
    }

    private void ReportInternal(Exception ex) =>
        _output.ErrorLine($"错误：{ErrorCodes.CliInternal}：{ex.Message}");

    private static string Join(string[] parts) => string.Join(' ', parts);
}