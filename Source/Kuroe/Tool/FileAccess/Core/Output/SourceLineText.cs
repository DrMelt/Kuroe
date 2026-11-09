using System.Text;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess.Output;

/// <summary>源行文本：可选行号，行号按最大行号右对齐，保留每行的终止符，截断行追加行内标注。</summary>
/// <param name="Lines">读取到的源行。</param>
/// <param name="LineNumbers">是否在每行前输出行号。</param>
public readonly record struct SourceLineText(IReadOnlyList<SourceLine> Lines, bool LineNumbers)
{
    /// <summary>行文本。不给行号时只输出内容与终止符。</summary>
    public override string ToString()
    {
        if (Lines.Count == 0)
        {
            return string.Empty;
        }

        if (!LineNumbers)
        {
            var raw = new StringBuilder();
            foreach (SourceLine line in Lines)
            {
                raw.Append(DisplayText(line));
                raw.Append(line.Terminator?.Value);
            }

            return raw.ToString();
        }

        int width = LineNumberFormat.Width(Lines.Max(line => line.Number));
        var text = new StringBuilder();
        foreach (SourceLine line in Lines)
        {
            LineNumberFormat.Append(text, line.Number, width, DisplayText(line));
            text.Append(line.Terminator?.Value);
        }

        return text.ToString();
    }

    /// <summary>行内容的展示文本。截断行在内容后标注原始行长。</summary>
    private static string DisplayText(SourceLine line) =>
        line.Truncated
            ? $"{line.Text}（本行共 {line.LineLength} 字符，已截断）"
            : line.Text;
}