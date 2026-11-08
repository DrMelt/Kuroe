using System.Globalization;
using System.Text;

namespace Kuroe.Tool.FileAccess.Output;

/// <summary>行号渲染：按最大行号的位数右对齐。</summary>
internal static class LineNumberFormat
{
    /// <summary>行号的渲染宽度，按最大行号的位数。</summary>
    public static int Width(int number) =>
        number.ToString(CultureInfo.InvariantCulture).Length;

    /// <summary>追加一行「行号 |内容」，行号按给定宽度右对齐。</summary>
    public static void Append(StringBuilder text, int number, int width, string content)
    {
        text.Append(number.ToString(CultureInfo.InvariantCulture).PadLeft(width));
        text.Append(" |");
        text.Append(content);
    }
}