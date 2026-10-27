using ErrorOr;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Process;
using Kuroe.Shared;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Command;

/// <summary>命令节点的运行期对象：消费引擎装入的输入，按路径变量解析执行目录并执行一次命令，成功时按唯一输出端口放出合并输出，失败停在阻塞。</summary>
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
    protected override string? Produce(NodeInputValues inputs)
    {
        string? failure = ResolveDirectory(inputs, out string workingDirectory);
        if (failure is not null)
        {
            return failure;
        }

        ProcessResult result;
        try
        {
            result = process.Run(
                Parameters.Program,
                Parameters.Arguments,
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

        if (result.ExitCode != 0)
        {
            return $"命令退出码 {result.ExitCode}：{Detail(output)}";
        }

        SetContent(Limit(output));

        return null;
    }

    /// <summary>按路径变量解析执行目录，未声明 Directory 时执行目录即路径基准。</summary>
    private string? ResolveDirectory(NodeInputValues inputs, out string workingDirectory)
    {
        workingDirectory = string.Empty;

        ErrorOr<PathResolution> resolved = PathVariables.Resolve(
            Parameters.Variables,
            port => inputs.Text(new PortName(port)),
            Parameters.RootDirectory);
        if (resolved.IsError)
        {
            return resolved.FirstError.Description;
        }

        if (Parameters.Directory is not { Length: > 0 } directory)
        {
            workingDirectory = resolved.Value.BaseDirectory;

            return null;
        }

        ErrorOr<string> expanded = PathVariables.Expand(
            directory,
            resolved.Value.Variables,
            resolved.Value.BaseDirectory,
            Parameters.RootDirectory,
            "Directory");
        if (expanded.IsError)
        {
            return expanded.FirstError.Description;
        }

        workingDirectory = expanded.Value;

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