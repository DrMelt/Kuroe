using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess;

/// <summary>按行读取文本，保留每行在原文中的终止符，并按单行行长上限截断。CRLF、CR 与 LF 都识别为行终止符。</summary>
public static class SourceLineReader
{
    /// <summary>读取指定行号范围，保留的行内容与终止符的字符数达到预算后停止收集。
    /// 行长达到单行上限时保留内容前缀并丢弃终止符。起始行超出文件长度时返回空行集合且 ToBeyondEnd 为真。</summary>
    /// <param name="reader">文本来源。</param>
    /// <param name="from">起始行号，从 1 起。</param>
    /// <param name="to">结束行号，不小于起始行号；为空表示读到文件尾。</param>
    /// <param name="characterBudget">保留的行内容与终止符的字符数预算，大于 0。</param>
    /// <param name="lineCharacterBudget">单行行长上限，含终止符，大于 0。</param>
    public static SourceLineReadResult Read(
        TextReader reader,
        int from,
        int? to,
        int characterBudget,
        int lineCharacterBudget)
    {
        SourceLineCursor cursor = new(reader);
        for (int number = 1; number < from; number++)
        {
            if (!cursor.TrySkipLine())
            {
                return new SourceLineReadResult([], ToBeyondEnd: true);
            }
        }

        List<SourceLine> lines = [];
        int characters = 0;
        bool toBeyondEnd = false;
        int count = to is { } end ? end - from + 1 : int.MaxValue;
        for (int index = 0; index < count; index++)
        {
            int number = from + index;
            if (!cursor.TryReadLine(out string text, out NewLineStyle? terminator))
            {
                toBeyondEnd = true;
                break;
            }

            SourceLine line = Limit(number, text, terminator, lineCharacterBudget);
            lines.Add(line);
            characters += line.Text.Length + (line.Terminator?.Value.Length ?? 0);
            if (characters > characterBudget)
            {
                break;
            }
        }

        return new SourceLineReadResult(lines, toBeyondEnd);
    }

    /// <summary>按单行上限生成源行。超限时保留内容前缀并丢弃终止符，原始行长写入结果。</summary>
    private static SourceLine Limit(
        int number,
        string text,
        NewLineStyle? terminator,
        int lineCharacterBudget)
    {
        int lineLength = text.Length + (terminator?.Value.Length ?? 0);
        if (lineLength <= lineCharacterBudget)
        {
            return new SourceLine(number, text, terminator);
        }

        return new SourceLine(number, TextBoundary.Take(text, lineCharacterBudget), null)
        {
            OriginalLineLength = lineLength,
        };
    }
}