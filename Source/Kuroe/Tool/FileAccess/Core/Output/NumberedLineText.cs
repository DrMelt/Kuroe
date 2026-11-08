using System.Text;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess.Output;

/// <summary>带行号的文本：行号按最大行号右对齐，行间用 LF。</summary>
/// <param name="Lines">带原始行号的行。</param>
public readonly record struct NumberedLineText(IReadOnlyList<NumberedLine> Lines)
{
    /// <summary>行文本，行间用 LF 分隔。</summary>
    public override string ToString()
    {
        if (Lines.Count == 0)
        {
            return string.Empty;
        }

        int width = LineNumberFormat.Width(Lines.Max(line => line.Number));
        var text = new StringBuilder();
        foreach (NumberedLine line in Lines)
        {
            if (text.Length > 0)
            {
                text.Append('\n');
            }

            LineNumberFormat.Append(text, line.Number, width, line.Text);
        }

        return text.ToString();
    }
}