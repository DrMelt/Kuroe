namespace Kuroe.Console.Views;

/// <summary>只读页：渲染页标题与内容后停在页内，刷新重印，返回或 Esc 回到上一级。调用方须持有独占呈现。</summary>
internal static class ViewPage
{
    private sealed record Item(string Label, string Action);

    private static readonly Item Back = new("← 返回上一级", "back");
    private static readonly Item Refresh = new("~ 刷新", "refresh");

    public static void Browse(Terminal terminal, string title, Action render)
    {
        while (true)
        {
            render();
            terminal.NewLine();

            Item picked = terminal.Choose(title, [Refresh, Back], item => item.Label, Back);
            if (picked.Action == "back")
            {
                return;
            }
        }
    }
}