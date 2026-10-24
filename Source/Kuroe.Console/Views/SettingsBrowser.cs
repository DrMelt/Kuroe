using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Configuration;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>设置菜单：列出生效配置，逐项写入或删除用户层设置。模型选择由目录菜单维护。</summary>
internal sealed class SettingsBrowser(
    SettingsProvider settings,
    Terminal terminal,
    ResultView results)
{
    /// <summary>一个菜单项。</summary>
    private sealed record Item(string Label, string Action, SettingEntry? Entry = null);

    private static readonly Item Back = new("← 返回", "back");
    private static readonly Item Refresh = new("~ 刷新", "refresh");

    public void Browse()
    {
        using (terminal.Exclusive())
        {
            while (true)
            {
                terminal.Page("主菜单", "设置");
                PrintConfig();
                terminal.NewLine();

                List<Item> items = [.. Entries().Select(entry => new Item(EntryLabel(entry), "edit", entry)), Refresh, Back];
                Item picked = terminal.Choose("设置项", items, item => item.Label, Back);
                if (picked.Action == "back")
                {
                    return;
                }

                if (picked.Entry is { } entry)
                {
                    Edit(entry);
                }
            }
        }
    }

    /// <summary>打印生效的配置，逐项标记是否来自用户层。</summary>
    public void PrintConfig()
    {
        terminal.Hint($"用户层文件：{settings.UserSettingsFile}");

        foreach (SettingSection section in settings.Sections)
        {
            Table table = Blocks.Table(("设置项", false), ("值", true), ("来源", false));
            foreach (SettingEntry entry in section.Entries)
            {
                table.AddRow(
                    new Text(entry.Name, Styles.Key),
                    new Text(entry.Value),
                    new Text(
                        entry.FromUserLayer ? "用户层已设置" : "用户层未设置",
                        entry.FromUserLayer ? Styles.Success : Styles.Hint));
            }

            terminal.NewLine();
            terminal.Write(Blocks.Section(BoxKind.View, section.Name, table));
        }
    }

    /// <summary>写入用户层中的设置项并立即生效。值按 JSON 字面量解析，不是合法 JSON 时按字符串写入。</summary>
    public void Set(string path, string value) => results.Apply(settings.Set(path, value));

    /// <summary>删除用户层中的该项。</summary>
    public void Unset(string path)
    {
        ErrorOr<string> resolved = SettingsProvider.ResolvePath(path);
        if (resolved.IsError)
        {
            results.Reject(resolved.ErrorsOrEmptyList);
            return;
        }

        if (settings.TryGetUserValue(resolved.Value) is null)
        {
            terminal.Line($"{resolved.Value} 未在用户层设置。");
            return;
        }

        results.Apply(settings.Clear(resolved.Value));
    }

    /// <summary>可编辑的设置项。模型选择由模型目录维护，库层拒绝按路径写入，不进入编辑列表。</summary>
    private IEnumerable<SettingEntry> Entries() =>
        settings.Sections
            .SelectMany(section => section.Entries)
            .Where(entry => !SettingsProvider.ResolvePath(entry.Path).IsError);

    private static string EntryLabel(SettingEntry entry) =>
        $"{entry.Path} · {entry.Value} · {(entry.FromUserLayer ? "用户层已设置" : "用户层未设置")}";

    private void Edit(SettingEntry entry)
    {
        List<Item> items = [new("* 写入新值", "set")];
        if (entry.FromUserLayer)
        {
            items.Add(new("- 删除用户层设置", "unset"));
        }

        items.Add(Back);

        Item picked = terminal.Choose($"{entry.Path} · 当前 {entry.Value}", items, item => item.Label, Back);
        switch (picked.Action)
        {
            case "set":
                string? value = terminal.Ask($"新值，当前 {Markup.Escape(entry.Value)}：");
                if (value is not null)
                {
                    Set(entry.Path, value);
                }

                break;

            case "unset":
                Unset(entry.Path);
                break;
        }
    }
}