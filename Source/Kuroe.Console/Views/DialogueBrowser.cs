using ErrorOr;
using Kuroe.Assistant.Dialogue;
using Kuroe.Catalogs;
using Kuroe.Configuration;

namespace Kuroe.Console.Views;

/// <summary>Kuroe 助手页：发送消息、切换模型与重置对话。发送消息期间连续读入，空行或 Esc 回到菜单。</summary>
internal sealed class DialogueBrowser(
    DialogueHost dialogue,
    ModelService models,
    CatalogBrowser catalogs,
    SettingsProvider settings,
    DialogueSink sink,
    Terminal terminal,
    ErrorView errors)
{
    /// <summary>助手回合的框标题。</summary>
    private const string SourceLabel = "Kuroe 助手";

    /// <summary>一个菜单项。</summary>
    private sealed record Item(string Label, string Action);

    private static readonly Item Back = new("← 返回主菜单", "back");

    public async Task BrowseAsync()
    {
        using (terminal.Exclusive())
        {
            while (true)
            {
                terminal.Page("主菜单", "Kuroe 助手");
                terminal.Line($"当前模型 {models.Current?.Value ?? "未选择"} · 回复语言 {settings.AssistantLanguage}");
                terminal.NewLine();

                Item[] items =
                [
                    new("→ 发送消息", "send"),
                    new("√ 切换模型", "model"),
                    new("- 重置对话", "reset"),
                    Back,
                ];

                Item picked = terminal.Choose("助手操作", items, item => item.Label, Back);
                switch (picked.Action)
                {
                    case "send":
                        await SendAsync();
                        break;

                    case "model":
                        catalogs.PickModel();
                        break;

                    case "reset":
                        Reset();
                        break;

                    case "back":
                        return;
                }
            }
        }
    }

    /// <summary>连续输入，空行或 Esc 返回助手菜单。</summary>
    private async Task SendAsync()
    {
        while (true)
        {
            terminal.NewLine();
            string? input = terminal.Ask("Kuroe >");
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            await ReplyAsync(input);
        }
    }

    private async Task ReplyAsync(string input)
    {
        ErrorOr<Success> outcome = Result.Success;
        string? failure = null;
        using (terminal.OpenFrame(BoxKind.Reply, SourceLabel))
        {
            try
            {
                outcome = await dialogue.ReplyAsync(input, sink);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }
        }

        if (failure is not null)
        {
            terminal.Error($"请求失败：{failure}");
            return;
        }

        if (outcome.IsError)
        {
            errors.Report(outcome.ErrorsOrEmptyList);
            errors.GuideModelRegistration(models, models.Current?.Value);
        }
    }

    private void Reset()
    {
        dialogue.Reset();
        terminal.Ok("对话已重置。");
    }
}