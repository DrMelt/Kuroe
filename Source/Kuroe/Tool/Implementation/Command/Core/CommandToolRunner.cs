using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ErrorOr;
using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Command;

/// <summary>命令模板的展开与执行。模板先按参数项拆分，占位符由实参填充，程序与参数经进程启动，输出文本交回模型。</summary>
internal static class CommandToolRunner
{
    /// <summary>按定义展开模板并执行命令，结果交回文本，错误以 ErrorOr 表达。工作目录缺省为项目工作目录根。</summary>
    public static ErrorOr<string> Execute(CommandToolDefinition definition, string baseDirectory, ToolArguments arguments)
    {
        ErrorOr<IReadOnlyList<string>> argv = Expand(definition, arguments);
        if (argv.IsError)
        {
            return argv.ErrorsOrEmptyList;
        }

        return RunProcess(definition, baseDirectory, argv.Value);
    }

    /// <summary>按定义展开模板得到命令行，第一项是程序本身。返回非空表示中断调用。</summary>
    internal static ErrorOr<IReadOnlyList<string>> Expand(CommandToolDefinition definition, ToolArguments arguments)
    {
        IReadOnlyList<ToolParameter> parameters = definition.Definition.Parameters;
        var argv = new List<string>();

        foreach (CommandToolTemplateItem item in definition.Template)
        {
            Error? rejected = ExpandItem(parameters, arguments, item, argv);
            if (rejected is not null)
            {
                return rejected.Value;
            }
        }

        if (argv.Count == 0)
        {
            return ToolErrors.Argument("模板展开后没有任何可执行的程序。");
        }

        return argv;
    }

    /// <summary>展开一个参数项。对象项的关联参数缺省时整项消失，必填仍拒绝；其余参数项按占位符展开。返回非空表示中断调用。</summary>
    private static Error? ExpandItem(IReadOnlyList<ToolParameter> parameters, ToolArguments arguments, CommandToolTemplateItem item, List<string> argv)
    {
        if (item.OmitWhenMissing is { } linkedName)
        {
            ToolParameter? linked = parameters.FirstOrDefault(candidate => candidate.Name.Value == linkedName);
            if (linked is not null && IsMissing(arguments, linked))
            {
                return linked.Required ? ToolErrors.Argument($"缺少参数 {linkedName}。") : null;
            }
        }

        if (CommandToolTemplate.PlaceholdersOf(item.Text).Count == 0)
        {
            argv.Add(item.Text);

            return null;
        }

        return ExpandToken(parameters, arguments, item.Text, argv);
    }

    /// <summary>关联参数是否缺省：布尔参数为真才算给出，列表参数为空算缺省，文本参数未给算缺省。</summary>
    private static bool IsMissing(ToolArguments arguments, ToolParameter parameter) => parameter switch
    {
        { Flag: true } => arguments.Flag(parameter.Name) != true,
        { List: true } => (arguments.List(parameter.Name) ?? []).Count == 0,
        _ => arguments.Text(parameter.Name) is null,
    };

    /// <summary>展开一个含占位符的参数项。展开后追加参数项，返回非空表示中断调用。</summary>
    private static Error? ExpandToken(IReadOnlyList<ToolParameter> parameters, ToolArguments arguments, string token, List<string> argv)
    {
        // 独立占位符：整个参数项就是 {name}。列表参数只允许这种写法。
        MatchCollection matches = CommandToolTemplate.Matches(token);
        if (matches.Count == 1 && token.StartsWith('{') && token.EndsWith('}'))
        {
            string name = matches[0].Groups[1].Value;
            ToolParameter? parameter = parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return ToolErrors.Internal($"模板占位符 {{{name}}} 没有对应参数声明。");
            }

            if (parameter.List)
            {
                List<string> values = [.. arguments.List(parameter.Name) ?? []];
                if (values.Count == 0)
                {
                    return parameter.Required ? ToolErrors.Argument($"缺少参数 {name}。") : null;
                }

                argv.AddRange(values);

                return null;
            }

            string? text = arguments.Text(parameter.Name);
            if (text is null)
            {
                return parameter.Required ? ToolErrors.Argument($"缺少参数 {name}。") : null;
            }

            argv.Add(text);

            return null;
        }

        // 内嵌占位符：只允许标量参数，逐个替换；可选参数缺省时该占位符按字面量保留。
        string expanded = token;
        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            ToolParameter? parameter = parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return ToolErrors.Internal($"模板占位符 {{{name}}} 没有对应参数声明。");
            }

            if (parameter.List)
            {
                return ToolErrors.Internal($"列表参数 {name} 只能写成独立的 {{{name}}} 参数项。");
            }

            string? value = arguments.Text(parameter.Name);
            if (value is null)
            {
                if (parameter.Required)
                {
                    return ToolErrors.Argument($"缺少参数 {name}。");
                }

                continue;
            }

            expanded = expanded.Replace(match.Value, value, StringComparison.Ordinal);
        }

        if (expanded.Length > 0)
        {
            argv.Add(expanded);
        }

        return null;
    }

    /// <summary>启动进程并等待结果，stdout 与 stderr 合并成文本交回，附退出码；超时或启动失败以 ErrorOr 表达。</summary>
    private static ErrorOr<string> RunProcess(CommandToolDefinition definition, string baseDirectory, IReadOnlyList<string> argv)
    {
        string working = definition.Directory is { Length: > 0 } directory
            ? Path.GetFullPath(directory, baseDirectory)
            : baseDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = argv[0],
            WorkingDirectory = working,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            CreateNoWindow = true,
        };
        foreach (string argument in argv.Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        object gate = new();
        var output = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendData(output, eventArgs.Data, gate);
        process.ErrorDataReceived += (_, eventArgs) => AppendData(output, eventArgs.Data, gate);

        try
        {
            if (!process.Start())
            {
                return ToolErrors.Execute($"无法启动 {argv[0]}。");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return ToolErrors.Execute($"无法启动 {argv[0]}：{ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        long timeoutMilliseconds = Math.Min(definition.TimeoutSeconds, CommandToolTemplate.MaxTimeoutSeconds) * 1000L;
        if (!process.WaitForExit((int)timeoutMilliseconds))
        {
            KillTree(process);
            process.WaitForExit(5000);
            return ToolErrors.Execute($"命令在 {definition.TimeoutSeconds} 秒内未结束，已终止。");
        }

        process.WaitForExit();

        string text = output.ToString().TrimEnd();
        string body = text.Length > definition.OutputLimit
            ? string.Concat(text.AsSpan(0, definition.OutputLimit), "…输出已截断")
            : text;

        return body.Length == 0
            ? $"命令已结束，退出码 {process.ExitCode}，无输出。"
            : $"{body}\n（退出码 {process.ExitCode}）";
    }

    /// <summary>stdout 与 stderr 的行汇入同一份文本，顺序不保。两路回调可能并行，写入加锁。</summary>
    private static void AppendData(StringBuilder output, string? data, object gate)
    {
        if (data is { Length: > 0 })
        {
            lock (gate)
            {
                output.AppendLine(data);
            }
        }
    }

    /// <summary>终止整个进程树，尽力而为，失败不阻断超时返回。</summary>
    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // 进程可能已退出或已被系统清理，不追加错误
        }
    }
}
