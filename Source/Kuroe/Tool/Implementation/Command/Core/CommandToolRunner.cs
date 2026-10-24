using ErrorOr;
using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Command;

/// <summary>命令的执行编排：展开模板，经进程端口启动命令，把输出文本交回模型。路径参数由工具函数在调用前校验。</summary>
internal sealed class CommandToolRunner
{
    private readonly CommandToolDefinition _definition;
    private readonly ICommandProcessRunner _process;
    private readonly string _workingDirectory;

    /// <summary>用定义、工作目录根与进程执行建立编排。</summary>
    public CommandToolRunner(
        CommandToolDefinition definition,
        string baseDirectory,
        ICommandProcessRunner process)
    {
        _definition = definition;
        _process = process;
        _workingDirectory = definition.Directory is { Length: > 0 } directory
            ? Path.GetFullPath(directory, baseDirectory)
            : baseDirectory;
        Paths = new ToolPathScope(_workingDirectory, baseDirectory, "工作目录");
    }

    /// <summary>路径参数的解析范围：实参按执行目录解析，根目录是工作目录根。</summary>
    public ToolPathScope Paths { get; }

    /// <summary>展开模板并执行命令，结果交回文本，错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> Execute(ToolArguments arguments)
    {
        ErrorOr<IReadOnlyList<string>> argv = _definition.CommandLine(arguments);
        if (argv.IsError)
        {
            return argv.ErrorsOrEmptyList;
        }

        return Invoke(argv.Value);
    }

    /// <summary>在工作目录执行展开后的命令行，格式化输出与退出码；超时或启动失败以 ErrorOr 表达。</summary>
    private ErrorOr<string> Invoke(IReadOnlyList<string> argv)
    {
        ErrorOr<CommandProcessResult> result = _process.Run(
            argv[0],
            [.. argv.Skip(1)],
            _workingDirectory,
            Timeout());

        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        if (result.Value.TimedOut)
        {
            return ToolErrors.Execute($"命令在 {_definition.TimeoutSeconds} 秒内未结束，已终止。");
        }

        string text = result.Value.Output.TrimEnd();
        string body = text.Length > _definition.OutputLimit
            ? string.Concat(text.AsSpan(0, _definition.OutputLimit), "…输出已截断")
            : text;

        return body.Length == 0
            ? $"命令已结束，退出码 {result.Value.ExitCode}，无输出。"
            : $"{body}\n（退出码 {result.Value.ExitCode}）";
    }

    /// <summary>等待上限，按定义收窄到毫秒换算不溢出的范围。</summary>
    private TimeSpan Timeout() =>
        TimeSpan.FromSeconds(Math.Min(_definition.TimeoutSeconds, CommandToolTemplate.MaxTimeoutSeconds));
}
