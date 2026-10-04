using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.CommandTools;

/// <summary>命令模板的展开与执行。模板先按参数项拆分，占位符由实参填充，程序与参数经进程启动，输出文本交回模型。</summary>
internal static partial class CommandToolRunner
{
    /// <summary>命令超时的缺省秒数。</summary>
    internal const int DefaultTimeoutSeconds = 120;

    /// <summary>命令超时的最大秒数，毫秒换算后不溢出等待参数。</summary>
    internal const int MaxTimeoutSeconds = int.MaxValue / 1000;

    /// <summary>交回文本的缺省上限。</summary>
    internal const int DefaultOutputLimit = 8000;

    [GeneratedRegex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    /// <summary>模板里出现的全部占位符名，按出现顺序并去重。</summary>
    internal static IReadOnlyList<string> PlaceholdersOf(IEnumerable<CommandToolTemplateItem> template) =>
        [.. template.SelectMany(PlaceholdersOf).Distinct()];

    /// <summary>一个参数项里出现的全部占位符名，按出现顺序。</summary>
    internal static IReadOnlyList<string> PlaceholdersOf(CommandToolTemplateItem item) =>
        [.. PlaceholdersOf(item.Text)];

    /// <summary>一段文字里出现的全部占位符名，按出现顺序。</summary>
    private static IEnumerable<string> PlaceholdersOf(string text) =>
        Placeholder().Matches(text).Select(match => match.Groups[1].Value);

    /// <summary>按定义展开模板并执行命令，结果与拒绝都写成文本。工作目录缺省为项目工作目录根。</summary>
    public static string Execute(CommandToolDefinition definition, string baseDirectory, ToolArguments arguments)
    {
        var argv = new List<string>();

        foreach (CommandToolTemplateItem item in definition.Template)
        {
            string? rejected = ExpandItem(definition, arguments, item, argv);
            if (rejected is not null)
            {
                return rejected;
            }
        }

        if (argv.Count == 0)
        {
            return "被拒绝：模板展开后没有任何可执行的程序。";
        }

        return RunProcess(definition, baseDirectory, argv);
    }

    /// <summary>展开一个参数项。对象项的关联参数缺省时整项消失，必填仍拒绝；其余参数项按占位符展开。</summary>
    private static string? ExpandItem(CommandToolDefinition definition, ToolArguments arguments, CommandToolTemplateItem item, List<string> argv)
    {
        if (item.OmitWhenMissing is { } linkedName)
        {
            ToolParameter? linked = definition.Parameters.FirstOrDefault(candidate => candidate.Name.Value == linkedName);
            if (linked is not null)
            {
                bool missing = linked.List
                    ? (arguments.List(linked.Name) ?? []).Count == 0
                    : arguments.Text(linked.Name) is null;

                if (missing)
                {
                    return linked.Required ? $"被拒绝：缺少参数 {linkedName}。" : null;
                }
            }
        }

        if (!Placeholder().IsMatch(item.Text))
        {
            argv.Add(item.Text);

            return null;
        }

        return ExpandToken(definition, arguments, item.Text, argv);
    }

    /// <summary>展开一个含占位符的参数项。展开后追加参数项，返回非空表示直接拒绝。</summary>
    private static string? ExpandToken(CommandToolDefinition definition, ToolArguments arguments, string token, List<string> argv)
    {
        // 独立占位符：整个参数项就是 {name}。列表参数只允许这种写法。
        MatchCollection matches = Placeholder().Matches(token);
        if (matches.Count == 1 && token.StartsWith('{') && token.EndsWith('}'))
        {
            string name = matches[0].Groups[1].Value;
            ToolParameter? parameter = definition.Parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return $"被拒绝：模板占位符 {{{name}}} 没有对应参数声明。";
            }

            if (parameter.List)
            {
                List<string> values = [.. arguments.List(parameter.Name) ?? []];
                if (values.Count == 0)
                {
                    return parameter.Required ? $"被拒绝：缺少参数 {name}。" : null;
                }

                argv.AddRange(values);

                return null;
            }

            string? text = arguments.Text(parameter.Name);
            if (text is null)
            {
                return parameter.Required ? $"被拒绝：缺少参数 {name}。" : null;
            }

            argv.Add(text);

            return null;
        }

        // 内嵌占位符：只允许标量参数，逐个替换；可选参数缺省时该占位符按字面量保留。
        string expanded = token;
        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            ToolParameter? parameter = definition.Parameters.FirstOrDefault(candidate => candidate.Name.Value == name);
            if (parameter is null)
            {
                return $"被拒绝：模板占位符 {{{name}}} 没有对应参数声明。";
            }

            if (parameter.List)
            {
                return $"被拒绝：列表参数 {name} 只能写成独立的 {{{name}}} 参数项。";
            }

            string? value = arguments.Text(parameter.Name);
            if (value is null)
            {
                if (parameter.Required)
                {
                    return $"被拒绝：缺少参数 {name}。";
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

    /// <summary>启动进程并等待结果，stdout 与 stderr 合并成文本交回，附退出码；超时或启动失败也写成文本。</summary>
    private static string RunProcess(CommandToolDefinition definition, string baseDirectory, List<string> argv)
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
                return $"被拒绝：无法启动 {argv[0]}。";
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return $"被拒绝：无法启动 {argv[0]}：{ex.Message}";
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        long timeoutMilliseconds = Math.Min(definition.TimeoutSeconds, MaxTimeoutSeconds) * 1000L;
        if (!process.WaitForExit((int)timeoutMilliseconds))
        {
            KillTree(process);
            process.WaitForExit(5000);
            return $"被拒绝：命令在 {definition.TimeoutSeconds} 秒内未结束，已终止。";
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
