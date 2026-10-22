using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli.Views;

/// <summary>固定结构的呈现：带标题的块、清单表与键值表。视图只给内容与列语义，排版由此处决定。</summary>
internal static class Blocks
{
    /// <summary>带标题的块。类别决定形状与线样式，标题按纯文本处理，为 null 时不画标题，动态文本一律经 IRenderable 传入内容。</summary>
    public static Panel Section(BoxKind kind, string? title, IRenderable content)
    {
        BoxSkin skin = BoxSkin.Of(kind);

        return new Panel(content)
        {
            Header = title is null ? null : new PanelHeader(Markup.Escape(title), Justify.Left),
            Border = skin.Shape,
            BorderStyle = skin.Line,
            Padding = BoxSkin.Padding,
            Expand = true,
        };
    }

    /// <summary>无边框清单表。首行是列名，wrap 决定该列是否换行。</summary>
    public static Table Table(params (string Header, bool Wrap)[] columns)
    {
        Table table = new()
        {
            Border = TableBorder.None,
            Expand = true,
        };

        foreach ((string header, bool wrap) in columns)
        {
            table.AddColumn(new TableColumn(new Text(header, Styles.Hint)) { NoWrap = !wrap });
        }

        return table;
    }

    /// <summary>两列键值表。标签列不换行，值列换行。</summary>
    public static Table Fields(params (string Label, IRenderable Value)[] fields)
    {
        Table table = new()
        {
            Border = TableBorder.None,
            Expand = true,
            ShowHeaders = false,
        };
        table.AddColumn(new TableColumn(new Text(string.Empty)) { NoWrap = true });
        table.AddColumn(new TableColumn(new Text(string.Empty)));

        foreach ((string label, IRenderable value) in fields)
        {
            table.AddRow(new Text(label, Styles.Hint), value);
        }

        return table;
    }
}