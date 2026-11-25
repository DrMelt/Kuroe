using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>框的类别。类别决定形状与线样式。</summary>
internal enum BoxKind
{
    /// <summary>详情与列表。</summary>
    View,

    /// <summary>等待按键的选择、确认与文本输入。</summary>
    Menu,
}

/// <summary>面板的外观：形状、线样式与内边距。等待按键的框用圆角，其余用方角。</summary>
internal sealed record BoxSkin(BoxBorder Shape, Style Line)
{
    public static readonly Padding Padding = new(1, 0, 1, 0);

    public static BoxSkin Of(BoxKind kind) => kind switch
    {
        BoxKind.Menu => new(BoxBorder.Rounded, Styles.BorderActive),
        _ => new(BoxBorder.Square, Styles.Border),
    };
}