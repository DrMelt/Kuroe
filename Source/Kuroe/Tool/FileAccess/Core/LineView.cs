using System.Globalization;
using System.Text;

namespace Kuroe.Tool.FileAccess;

/// <summary>行渲染：可选行号，行号按最大行号右对齐，正文不截断。</summary>
public static class LineView
{
    /// <summary>把带原始行号与终止符的行渲染成「行号 |内容」加该行终止符；不给行号时只输出内容与终止符。</summary>
    public static string Format(IReadOnlyList<SourceLine> lines, bool lineNumbers = true)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        if (!lineNumbers)
        {
            var raw = new StringBuilder();
            foreach (SourceLine line in lines)
            {
                raw.Append(line.Text);
                raw.Append(line.NewLine);
            }

            return raw.ToString();
        }

        int width = Width(lines.Max(line => line.Number));
        var text = new StringBuilder();
        foreach (SourceLine line in lines)
        {
            Append(text, line.Number, width, line.Text);
            text.Append(line.NewLine);
        }

        return text.ToString();
    }

    /// <summary>把带原始行号的行渲染成「行号 |内容」，行间用 LF。</summary>
    public static string Format(IReadOnlyList<NumberedLine> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        int width = Width(lines.Max(line => line.Number));
        var text = new StringBuilder();
        foreach (NumberedLine line in lines)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            Append(text, line.Number, width, line.Text);
        }

        return text.ToString();
    }

    /// <summary>行号的渲染宽度，按最大行号的位数。</summary>
    private static int Width(int number) =>
        number.ToString(CultureInfo.InvariantCulture).Length;

    /// <summary>追加一行「行号 |内容」，行号按给定宽度右对齐。</summary>
    private static void Append(StringBuilder text, int number, int width, string content)
    {
        text.Append(number.ToString(CultureInfo.InvariantCulture).PadLeft(width));
        text.Append(" |");
        text.Append(content);
    }
}