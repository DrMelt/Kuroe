using Kuroe.Assistant.Dialogue;
using Spectre.Console;
using Spectre.Console.Rendering;
using Kuroe.Kernel.Execution.Shared.Conversation;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Console.Views;

/// <summary>会话历史的终端呈现：用户与助手分区块显示，只给出两方的文本，思考与工具内容不显示。</summary>
internal sealed class DialogueHistoryView(Terminal terminal)
{
    private const int MessageLimit = 40;
    private const int TailLimit = 20;
    private const string Title = "会话历史";
    private const string UserTitle = "用户";
    private const string AssistantTitle = "Kuroe 助手";

    public void Print(DialogueHistory history)
    {
        List<Entry> entries = [];
        if (history.Errors.Count > 0)
        {
            entries.Add(Hint($"历史读取失败：{string.Join("；", history.Errors.Select(error => error.Description))}", Styles.Warning));
        }

        List<Entry> messages = [];
        foreach (ExecutionMessage message in history.Conversation.Messages)
        {
            if (message.Role is not (ExecutionMessageRole.User or ExecutionMessageRole.Assistant))
            {
                continue;
            }

            string text = TextOf(message);
            if (text.Length == 0)
            {
                continue;
            }

            messages.Add(Message(
                message.Role == ExecutionMessageRole.User ? UserTitle : AssistantTitle,
                text,
                Style.Plain));
        }

        if (messages.Count > MessageLimit)
        {
            messages = [.. messages.Skip(messages.Count - MessageLimit)];
            entries.Add(Hint("更早的消息未显示。", Styles.Hint));
        }

        entries.AddRange(messages);

        IReadOnlyList<ExecutionSnapshot> tail = history.Tail;
        if (tail.Count > TailLimit)
        {
            tail = [.. tail.Skip(tail.Count - TailLimit)];
            entries.Add(Hint("更早的未完成轮次未显示。", Styles.Hint));
        }

        foreach (ExecutionSnapshot execution in tail)
        {
            string input = execution.Request.Inputs.Count > 0 ? execution.Request.Inputs[0].Text : string.Empty;
            if (input.Length > 0)
            {
                entries.Add(Message(UserTitle, input, Style.Plain));
            }

            entries.Add(Message(AssistantTitle, StateText(execution), ViewLabels.StyleOf(execution.State)));
        }

        if (entries.Count == 0)
        {
            terminal.Hint("还没有对话。");

            return;
        }

        terminal.Write(Blocks.Section(
            BoxKind.View,
            Title,
            new Rows(entries.Select(Render))));
    }

    private static IRenderable Render(Entry entry) =>
        entry.Heading is { } heading
            ? Blocks.Section(BoxKind.View, heading, Content(entry.Body, entry.Style))
            : new Text(entry.Body, entry.Style);

    private static Paragraph Content(string text, Style style)
    {
        Paragraph paragraph = new();
        paragraph.Append(text, style);

        return paragraph;
    }

    private static string StateText(ExecutionSnapshot execution)
    {
        string state = $"〔{execution.State.Label()}〕";

        return execution.Failures.Count == 0
            ? state
            : $"{state}{string.Join("；", execution.Failures)}";
    }

    private static Entry Hint(string text, Style style) => new(null, text, style);

    private static Entry Message(string title, string text, Style style) => new(title, text, style);

    private static string TextOf(ExecutionMessage message) =>
        string.Concat(message.Parts
            .Where(part => part.Kind == ExecutionContentKind.Text)
            .Select(part => part.Text));

    private sealed record Entry(string? Heading, string Body, Style Style);
}