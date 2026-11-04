using ErrorOr;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Process;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Command;

/// <summary>命令节点的运行期对象：消费引擎装入的输入，按命令变量展开执行目录与参数并执行一次命令，成功时按唯一输出端口放出合并输出，失败停在阻塞。</summary>
internal sealed class CommandRuntime(ExecutableNode executable, CommandParameters parameters, IProcessRunner process)
    : SelfDrivenRuntime(executable)
{
    /// <summary>本节点的类型专属声明。</summary>
    private CommandParameters Parameters { get; } = parameters;

    /// <inheritdoc/>
    protected override string Action => "执行";

    /// <inheritdoc/>
    protected override string Target => Parameters.Program;

    /// <inheritdoc/>
    protected override string? Produce(INodeInputs inputs)
    {
        ErrorOr<CommandVariableValues> variables = CommandVariables.Resolve(
            Parameters.Variables,
            port => inputs.Text(new PortName(port)),
            Parameters.RootDirectory);
        if (variables.IsError)
        {
            return variables.FirstError.Description;
        }

        string workingDirectory = variables.Value.BaseDirectory;
        if (Parameters.Directory is { Length: > 0 } directory)
        {
            ErrorOr<string> expanded = CommandVariables.ExpandDirectory(
                directory,
                variables.Value,
                Parameters.RootDirectory);
            if (expanded.IsError)
            {
                return expanded.FirstError.Description;
            }

            workingDirectory = expanded.Value;
        }

        ErrorOr<IReadOnlyList<string>> arguments = CommandVariables.ExpandArguments(
            Parameters.Arguments,
            variables.Value.Variables);
        if (arguments.IsError)
        {
            return arguments.FirstError.Description;
        }

        ProcessResult result;
        try
        {
            result = process.Run(
                Parameters.Program,
                arguments.Value,
                workingDirectory,
                TimeSpan.FromSeconds(Parameters.TimeoutSeconds));
        }
        catch (ProcessStartException ex)
        {
            return ex.Message;
        }

        string output = result.Output.TrimEnd();
        if (result.TimedOut)
        {
            return $"命令在 {Parameters.TimeoutSeconds} 秒内未结束，已终止。";
        }

        if (result.ExitCode != 0 && Parameters.FailOnNonZeroExitCode)
        {
            return $"命令退出码 {result.ExitCode}：{Detail(output)}";
        }

        SetContent(Limit(output));

        return null;
    }

    /// <summary>输出按声明上限截断，0 表示不限制。</summary>
    private string Limit(string text) =>
        Parameters.MaxChars > 0 && text.Length > Parameters.MaxChars
            ? string.Concat(text.AsSpan(0, Parameters.MaxChars), "…输出已截断")
            : text;

    /// <summary>失败说明里的输出按同一上限截断，没有输出时给出说明。</summary>
    private string Detail(string text) => text.Length > 0 ? Limit(text) : "无输出。";
}