using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Configuration;
using Spectre.Console;

namespace Kuroe.Cli.Commands;

/// <summary>/config、/set、/unset 的解析与执行。模型路径由 /model 管理，库层一律拒绝按路径写入。</summary>
internal sealed class SettingsCommands(
    SettingsProvider settings,
    Terminal terminal,
    ResultPrinter results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/config", "打印生效配置，标记各项是否已写入用户层"),
        ("/set <路径> <值>", "写入用户层并立即生效"),
        ("/unset <路径>", "删除用户层中的该项"),
    ];

    private readonly SettingsProvider _settings = settings;
    private readonly Terminal _terminal = terminal;
    private readonly ResultPrinter _results = results;

    /// <summary>写入用户层中的设置项并立即生效。值按 JSON 字面量解析，不是合法 JSON 时按字符串写入。</summary>
    public void Set(string[] parts)
    {
        if (parts.Length != 3)
        {
            _terminal.Hint("用法：/set <路径> <值>，值含空格时用双引号包起来，例如 /set Agent:Temperature 0.7");
            return;
        }

        _results.Apply(_settings.Set(parts[1], parts[2]));
    }

    /// <summary>删除用户层中的该项。</summary>
    public void Unset(string[] parts)
    {
        if (parts.Length != 2)
        {
            _terminal.Hint("用法：/unset <路径>，例如 /unset Agent:Temperature");
            return;
        }

        ErrorOr<string> resolved = SettingsProvider.ResolvePath(parts[1]);
        if (resolved.IsError)
        {
            _results.Reject(resolved.ErrorsOrEmptyList);
            return;
        }

        if (_settings.TryGetUserValue(resolved.Value) is null)
        {
            _terminal.Line($"{resolved.Value} 未在用户层设置。");
            return;
        }

        _results.Apply(_settings.Clear(resolved.Value));
    }

    /// <summary>打印生效的配置，逐项标记是否来自用户层。</summary>
    public void PrintConfig()
    {
        _terminal.Hint($"用户层文件：{_settings.UserSettingsFile}");

        foreach (SettingSection section in _settings.Sections())
        {
            _terminal.Line($"{section.Name}:");

            Grid grid = Terminal.Columns(3, wrapColumns: 1);
            foreach (SettingEntry entry in section.Entries)
            {
                grid.AddRow(
                    new Text(entry.Name, Styles.Key),
                    new Text(entry.Value),
                    new Text(
                        entry.FromUserLayer ? "用户层已设置" : "用户层未设置",
                        entry.FromUserLayer ? Styles.Success : Styles.Hint));
            }

            _terminal.Write(grid);
        }
    }
}
