using ErrorOr;
using Kuroe.Host.Assistant.Dialogue;
using Kuroe.Host.Composition.Catalogs;
using Kuroe.Host.Composition.Configuration;
using Kuroe.Kernel.Tasks.Shared;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>Kuroe 助手页：会话页显示历史与操作，会话列表负责新建与选择。</summary>
internal sealed class DialogueBrowser(
    DialogueHost dialogue,
    ModelService models,
    CatalogBrowser catalogs,
    SettingsProvider settings,
    Terminal terminal,
    ErrorView errors)
{
    private const string SourceLabel = "Kuroe 助手";

    private bool _subscribed;
    private Message? _message;

    private sealed record Message(string Text, bool Success);

    private sealed record Item(
        string Label,
        string Action,
        TaskId? Session = null);

    private static readonly Item Back = new("← 返回主菜单", "back");
    private static readonly Item Send = new("→ 发送消息", "send");
    private static readonly Item ModelItem = new("√ 切换模型", "model");
    private static readonly Item Mode = new("↔ 切换模式", "mode");
    private static readonly Item RenameItem = new("✎ 重命名会话", "rename");
    private static readonly Item DeleteItem = new("× 删除会话", "delete");
    private static readonly Item List = new("≡ 会话列表", "list");
    private static readonly Item Refresh = new("~ 刷新", "refresh");

    public async Task BrowseAsync()
    {
        using (terminal.Exclusive())
        {
            if (!_subscribed)
            {
                dialogue.TurnCompleted += OnTurnCompleted;
                _subscribed = true;
            }

            ErrorOr<DialogueSessionInfo> session = EnsureSession();
            if (session.IsError)
            {
                errors.Report(session.ErrorsOrEmptyList);
                errors.GuideModelRegistration(models, models.Current?.Value);
                return;
            }

            await OpenAsync(session.Value.Id);
            _message = null;
        }
    }

    private async Task OpenAsync(TaskId id)
    {
        bool redraw = true;
        while (true)
        {
            DialogueSessionInfo? session = dialogue.Sessions.FirstOrDefault(candidate => candidate.Id == id)
                ?? dialogue.Active;
            if (session is null)
            {
                return;
            }

            if (redraw)
            {
                terminal.Page("主菜单", "Kuroe 助手", session.Title);
                terminal.Line($"当前模型 {session.Model} · 回复语言 {session.Language} · 模式 {session.Mode.DisplayName} · 状态 {StatusOf(session)}");
                terminal.NewLine();

                ErrorOr<DialogueHistory> history = await dialogue.ReadHistoryAsync(session.Id);
                if (history.IsError)
                {
                    errors.Report(history.ErrorsOrEmptyList);
                }
                else
                {
                    new DialogueHistoryView(terminal).Print(history.Value);
                }

                if (_message is { } message)
                {
                    if (message.Success)
                    {
                        terminal.Ok(message.Text);
                    }
                    else
                    {
                        terminal.Hint(message.Text);
                    }

                    _message = null;
                }

                terminal.NewLine();
            }

            redraw = true;
            Item[] items = [Refresh, Send, ModelItem, Mode, RenameItem, DeleteItem, List, Back];
            Item picked = terminal.Choose("助手操作", items, item => item.Label, Back);
            switch (picked.Action)
            {
                case "send":
                    redraw = StartTurn(session);
                    break;

                case "model":
                    catalogs.PickModel();
                    _message = new("模型只影响新建会话。", false);
                    break;

                case "mode":
                    PickMode(session);
                    break;

                case "rename":
                    Rename(session);
                    break;

                case "delete":
                    if (await DeleteAsync(session))
                    {
                        if (dialogue.Active is { } next)
                        {
                            id = next.Id;
                            break;
                        }

                        return;
                    }

                    break;

                case "list":
                    if (BrowseSessions() is { } selected)
                    {
                        id = selected;
                    }

                    break;

                case "refresh":
                    break;

                case "back":
                    return;
            }
        }
    }
    /// <summary>取当前会话，没有时新建。模型未选择时返回模型错误。</summary>
    private ErrorOr<DialogueSessionInfo> EnsureSession()
    {
        if (dialogue.Active is { } active)
        {
            return active;
        }

        IReadOnlyList<DialogueSessionInfo> sessions = dialogue.Sessions;
        if (sessions.Count > 0)
        {
            DialogueSessionInfo existing = sessions[0];
            ErrorOr<Success> selected = dialogue.Select(existing.Id);

            return selected.IsError ? selected.ErrorsOrEmptyList : existing;
        }

        return dialogue.Create();
    }

    /// <summary>读入一条消息后交给后台轮次，立即回到会话页。</summary>
    private bool StartTurn(DialogueSessionInfo session)
    {
        if (session.Busy)
        {
            terminal.Warn("会话正在执行，等本轮结束后再发送。");

            return false;
        }

        terminal.NewLine();
        string? input = terminal.Ask("Kuroe >");
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        _ = RunTurnAsync(session.Id, input);

        return true;
    }

    private async Task RunTurnAsync(TaskId id, string input)
    {
        try
        {
            await dialogue.SendAsync(id, input);
        }
        catch (Exception ex)
        {
            terminal.Notice($"{SourceLabel} 轮次异常：{ex.Message}", Styles.Error);
        }
    }

    private void OnTurnCompleted(DialogueTurnNotice notice)
    {
        string text = notice.Succeeded
            ? $"会话 #{notice.Session.Short} 已完成。"
            : $"会话 #{notice.Session.Short} 失败：{string.Join("；", notice.Errors.Select(error => error.Description))}";
        terminal.Notice(text, notice.Succeeded ? Styles.Success : Styles.Error);
    }

    /// <summary>会话列表：显示全部会话，选择后回到会话页；也可以新建会话。</summary>
    private TaskId? BrowseSessions()
    {
        while (true)
        {
            terminal.Page("主菜单", "Kuroe 助手", "会话列表");
            terminal.Line($"当前模型 {models.Current?.Value ?? "未选择"} · 回复语言 {settings.AssistantLanguage}");
            terminal.NewLine();

            IReadOnlyList<DialogueSessionInfo> sessions = dialogue.Sessions;
            PrintSessions(sessions);
            terminal.NewLine();

            List<Item> items = [.. sessions.Select(session => new Item(
                    $"› {SessionLabel(session)}",
                    "open",
                    session.Id))];
            items.Add(new Item("+ 新建会话", "new"));
            items.Add(Back);

            Item picked = terminal.Choose("会话", items, item => item.Label, Back);
            switch (picked.Action)
            {
                case "open" when picked.Session is { } id:
                    ErrorOr<Success> selected = dialogue.Select(id);
                    if (selected.IsError)
                    {
                        errors.Report(selected.ErrorsOrEmptyList);
                        break;
                    }

                    return id;

                case "new":
                    ErrorOr<DialogueSessionInfo> created = dialogue.Create();
                    if (created.IsError)
                    {
                        errors.Report(created.ErrorsOrEmptyList);
                        errors.GuideModelRegistration(models, models.Current?.Value);
                        break;
                    }

                    return created.Value.Id;

                case "back":
                    return null;
            }
        }
    }

    private void PrintSessions(IReadOnlyList<DialogueSessionInfo> sessions)
    {
        if (sessions.Count == 0)
        {
            terminal.Hint("还没有会话，新建一个会话开始对话。");
            return;
        }

        Table table = Blocks.Table(
            ("#", false),
            ("标题", true),
            ("模型", false),
            ("模式", false),
            ("状态", false),
            ("最近", false));
        foreach (DialogueSessionInfo session in sessions)
        {
            table.AddRow(
                new Text($"#{session.Id.Short}", Styles.Key),
                new Text(ViewLabels.OneLine(session.Title, 30)),
                new Text(session.Model),
                new Text(session.Mode.DisplayName),
                new Text(StatusOf(session), StatusStyleOf(session)),
                new Text(ViewLabels.Clock(session.LastActivityAt), Styles.Hint));
        }

        terminal.Write(Blocks.Section(BoxKind.View, $"会话 {sessions.Count} 个", table));
    }
    /// <summary>选择模式，取消保持当前模式。切换在下一轮生效，不重建会话。</summary>
    private void PickMode(DialogueSessionInfo session)
    {
        DialogueMode current = session.Mode;
        DialogueMode picked = terminal.Choose("选择模式", DialogueMode.All, mode => mode.DisplayName, current);
        if (ReferenceEquals(picked, current))
        {
            return;
        }

        ErrorOr<Success> result = dialogue.SelectMode(session.Id, picked);
        if (result.IsError)
        {
            errors.Report(result.ErrorsOrEmptyList);
            return;
        }

        _message = new($"模式已切换为{picked.DisplayName}，下一轮生效。", true);
    }

    private void Rename(DialogueSessionInfo session)
    {
        terminal.NewLine();
        string? title = terminal.Ask("新标题");
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        ErrorOr<Success> renamed = dialogue.Rename(session.Id, title);
        if (renamed.IsError)
        {
            errors.Report(renamed.ErrorsOrEmptyList);
            return;
        }

        _message = new("会话已重命名。", true);
    }

    private async Task<bool> DeleteAsync(DialogueSessionInfo session)
    {
        if (!Confirmation.Ask(
            terminal,
            $"删除会话 {session.Title}？历史与存档会一并删除。",
            "× 确认删除",
            "← 返回，保留会话"))
        {
            return false;
        }

        ErrorOr<Success> deleted = await dialogue.DeleteAsync(session.Id);
        if (deleted.IsError)
        {
            errors.Report(deleted.ErrorsOrEmptyList);
            return false;
        }

        _message = new("会话已删除。", true);
        return true;
    }

    private static string StatusOf(DialogueSessionInfo session)
    {
        if (session.Busy)
        {
            return "执行中";
        }

        return session.State == TaskState.Running ? "等待输入" : ViewLabels.Of(session.State);
    }

    private static Style StatusStyleOf(DialogueSessionInfo session)
    {
        if (session.Busy)
        {
            return Styles.Key;
        }

        return session.State == TaskState.Running ? Styles.Hint : ViewLabels.StyleOf(session.State);
    }

    private static string SessionLabel(DialogueSessionInfo session) =>
        $"#{session.Id.Short} {ViewLabels.OneLine(session.Title, 30)} · {session.Model} · {StatusOf(session)}";
}