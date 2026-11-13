using ErrorOr;
using Kuroe.Flow.Execution.Shared;
using Kuroe.Process;

namespace Kuroe.Node.Command;

/// <summary>命令节点的一次执行：按命令变量展开执行目录与参数，执行一次命令并给出合并输出。失败原因随执行交回。</summary>
internal sealed class CommandExecutionRunner(CommandParameters parameters, IProcessRunner process) : IExecutionRunner
{
    private readonly ExecutionJournal _journal = new();

    /// <summary>本次执行的过程记录。</summary>
    public IProcessJournal Journal => _journal;

    /// <summary>执行一次命令，成功时给出合并输出。</summary>
    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ErrorOr<CommandVariableValues> variables = CommandVariables.Resolve(
            parameters.Variables,
            port => request.Texts.GetValueOrDefault(new ExecutionPortName(port)),
            parameters.RootDirectory);
        if (variables.IsError)
        {
            return Failure(variables.FirstError.Description);
        }

        string workingDirectory = variables.Value.BaseDirectory;
        if (parameters.Directory is { Length: > 0 } directory)
        {
            ErrorOr<string> expanded = CommandVariables.ExpandDirectory(
                directory,
                variables.Value,
                parameters.RootDirectory);
            if (expanded.IsError)
            {
                return Failure(expanded.FirstError.Description);
            }

            workingDirectory = expanded.Value;
        }

        ErrorOr<IReadOnlyList<string>> arguments = CommandVariables.ExpandArguments(
            parameters.Arguments,
            variables.Value.Variables);
        if (arguments.IsError)
        {
            return Failure(arguments.FirstError.Description);
        }

        _journal.ReportProgress($"执行 {parameters.Program}");
        ProcessResult result;
        try
        {
            result = await process.RunAsync(
                parameters.Program,
                arguments.Value,
                workingDirectory,
                TimeSpan.FromSeconds(parameters.TimeoutSeconds),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ProcessStartException ex)
        {
            return Failure(ex.Message);
        }

        string output = result.Output.TrimEnd();
        if (result.TimedOut)
        {
            return Failure($"命令在 {parameters.TimeoutSeconds} 秒内未结束，已终止。");
        }

        if (result.ExitCode != 0 && parameters.FailOnNonZeroExitCode)
        {
            return Failure($"命令退出码 {result.ExitCode}：{Detail(output)}");
        }

        string content = Limit(output);
        if (content.Length > 0)
        {
            _journal.AppendContent(ProcessContentKind.Reply, content);
        }
        else
        {
            _journal.Append("输出", "无输出。");
        }

        return ExecutionOutput.FromContent(content);
    }

    /// <summary>输出按声明上限截断，0 表示不限制。</summary>
    private string Limit(string text) =>
        parameters.MaxChars > 0 && text.Length > parameters.MaxChars
            ? string.Concat(text.AsSpan(0, parameters.MaxChars), "…输出已截断")
            : text;

    /// <summary>失败说明里的输出按同一上限截断，没有输出时给出说明。</summary>
    private string Detail(string text) => text.Length > 0 ? Limit(text) : "无输出。";

    /// <summary>记下失败原因并给出失败的执行结果。</summary>
    private ErrorOr<ExecutionOutput> Failure(string message)
    {
        _journal.Append("命令", message, JournalLevel.Warning);

        return Error.Failure("Command.Failed", message);
    }
}