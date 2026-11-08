using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess.Output;

/// <summary>按行号范围读取的输出：行渲染、截断、范围标记与文件尾提示。</summary>
/// <param name="Read">读取结果。</param>
/// <param name="LineNumbers">是否在每行前输出行号。</param>
public readonly record struct ReadRangeText(SourceLineReadResult Read, bool LineNumbers)
{
    /// <summary>读取结果文本。截断提示与文件尾提示放在范围标记之前，结束标记后不追加换行。</summary>
    public override string ToString()
    {
        if (Read.Lines.Count == 0)
        {
            return "（已到文件结尾，文件没有内容）";
        }

        LimitedText limited = new(new SourceLineText(Read.Lines, LineNumbers).ToString());
        string content = new MarkedText(limited.Body).ToString();

        if (limited.Truncated)
        {
            return new NoticeText(LimitedText.Notice, content).ToString();
        }

        if (!Read.ToBeyondEnd)
        {
            return content;
        }

        int first = Read.Lines[0].Number;
        int last = Read.Lines[Read.Lines.Count - 1].Number;
        string range = first == last ? $"第 {first} 行" : $"第 {first} 到 {last} 行";
        return new NoticeText($"（已到文件结尾，实际返回{range}）", content).ToString();
    }
}