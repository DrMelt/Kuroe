namespace Kuroe.Console.Views;

/// <summary>从一组文本候选中选一项，取消返回 null。菜单至少要有两项，因此取消项单独加入。</summary>
internal static class Choices
{
    private sealed record Choice(string Label, int Index);

    private static readonly Choice Cancel = new("← 返回", -1);

    public static int? Pick(Terminal terminal, string title, IReadOnlyList<string> labels)
    {
        List<Choice> items = [.. labels.Select((label, index) => new Choice(label, index)), Cancel];
        Choice picked = terminal.Choose(title, items, item => item.Label, Cancel);

        return picked.Index >= 0 ? picked.Index : null;
    }
}