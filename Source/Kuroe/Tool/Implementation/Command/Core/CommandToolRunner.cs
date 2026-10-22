using ErrorOr;
using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Command;

/// <summary>命令的执行编排：按定义校验路径实参、展开模板，经进程端口启动命令，把输出文本交回模型。</summary>
internal sealed class CommandToolRunner(
    CommandToolDefinition definition,
    string baseDirectory,
    ICommandProcessRunner process)
{
    /// <summary>校验路径实参、展开模板并执行命令，结果交回文本，错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> Execute(ToolArguments arguments)
    {
        string working = WorkingDirectory();

        Error? rejected = CheckPaths(working, arguments);
        if (rejected is not null)
        {
            return rejected.Value;
        }

        ErrorOr<IReadOnlyList<string>> argv = definition.CommandLine(arguments);
        if (argv.IsError)
        {
            return argv.ErrorsOrEmptyList;
        }

        return Invoke(working, argv.Value);
    }

    /// <summary>命令执行目录：Directory 相对工作目录解析，缺省为工作目录根。</summary>
    private string WorkingDirectory() =>
        definition.Directory is { Length: > 0 } directory
            ? Path.GetFullPath(directory, baseDirectory)
            : baseDirectory;

    /// <summary>校验全部路径实参。列表逐项校验，任一项不通过即返回错误。</summary>
    private Error? CheckPaths(string workingDirectory, ToolArguments arguments)
    {
        foreach (CommandToolParameter parameter in definition.Parameters)
        {
            if (!parameter.Path)
            {
                continue;
            }

            CommandToolPathAccess access = parameter.PathAccess ?? CommandToolPathAccess.Empty;
            ToolName name = new(parameter.Name);

            if (parameter.List)
            {
                foreach (string value in arguments.List(name) ?? [])
                {
                    Error? rejected = CheckPath(access, value, workingDirectory);
                    if (rejected is not null)
                    {
                        return rejected;
                    }
                }

                continue;
            }

            if (arguments.Text(name) is { } text)
            {
                Error? rejected = CheckPath(access, text, workingDirectory);
                if (rejected is not null)
                {
                    return rejected;
                }
            }
        }

        return null;
    }

    /// <summary>校验一个路径值。实参按执行目录解析，根目录是工作目录根，通过时返回 null。</summary>
    private Error? CheckPath(CommandToolPathAccess access, string value, string workingDirectory)
    {
        ErrorOr<Success> checkedValue = access.Check(value, workingDirectory, baseDirectory);

        return checkedValue.IsError ? checkedValue.FirstError : null;
    }

    /// <summary>在工作目录执行展开后的命令行，格式化输出与退出码；超时或启动失败以 ErrorOr 表达。</summary>
    private ErrorOr<string> Invoke(string workingDirectory, IReadOnlyList<string> argv)
    {
        ErrorOr<CommandProcessResult> result = process.Run(
            argv[0],
            [.. argv.Skip(1)],
            workingDirectory,
            Timeout());

        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        if (result.Value.TimedOut)
        {
            return ToolErrors.Execute($"命令在 {definition.TimeoutSeconds} 秒内未结束，已终止。");
        }

        string text = result.Value.Output.TrimEnd();
        string body = text.Length > definition.OutputLimit
            ? string.Concat(text.AsSpan(0, definition.OutputLimit), "…输出已截断")
            : text;

        return body.Length == 0
            ? $"命令已结束，退出码 {result.Value.ExitCode}，无输出。"
            : $"{body}\n（退出码 {result.Value.ExitCode}）";
    }

    /// <summary>等待上限，按定义收窄到毫秒换算不溢出的范围。</summary>
    private TimeSpan Timeout() =>
        TimeSpan.FromSeconds(Math.Min(definition.TimeoutSeconds, CommandToolTemplate.MaxTimeoutSeconds));
}
