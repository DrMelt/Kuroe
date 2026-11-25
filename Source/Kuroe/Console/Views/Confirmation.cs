namespace Kuroe.Console.Views;

/// <summary>需要再确认的动作：默认停在保留现状的选项，只有选中确认项才返回 true。</summary>
internal static class Confirmation
{
    private sealed record Choice(string Label, bool Accepted);

    public static bool Ask(Terminal terminal, string title, string confirmLabel, string keepLabel)
    {
        Choice keep = new(keepLabel, false);
        Choice confirm = new(confirmLabel, true);
        Choice picked = terminal.Choose(title, [keep, confirm], choice => choice.Label, keep);

        return picked.Accepted;
    }
}