using System.Text;

namespace Kuroe.Tool.FileAccess;

/// <summary>按行读取文本，保留每行在原文中的终止符。CRLF、CR 与 LF 都识别为行终止符。</summary>
public static class SourceLineReader
{
    /// <summary>单次从文本来源读取的字符数。</summary>
    private const int BufferSize = 4096;

    /// <summary>读取指定行号范围，行内容与终止符的字符数达到预算后停止收集。</summary>
    /// <param name="reader">文本来源。</param>
    /// <param name="from">起始行号，从 1 起。</param>
    /// <param name="to">结束行号，不小于起始行号。</param>
    /// <param name="characterBudget">行内容与终止符的字符数预算，超出后停止收集。</param>
    public static SourceLineReadResult Read(TextReader reader, int from, int to, int characterBudget)
    {
        Cursor cursor = new(reader);
        for (int number = 1; number < from; number++)
        {
            if (!cursor.TrySkipLine())
            {
                return new SourceLineReadResult([], FromBeyondEnd: true, ToBeyondEnd: true);
            }
        }

        List<SourceLine> lines = [];
        int characters = 0;
        bool toBeyondEnd = false;
        int count = to - from + 1;
        for (int index = 0; index < count; index++)
        {
            int number = from + index;
            if (!cursor.TryReadLine(out string text, out string newLine))
            {
                toBeyondEnd = true;
                break;
            }

            lines.Add(new SourceLine(number, text, newLine));
            characters += text.Length + newLine.Length;
            if (characters > characterBudget)
            {
                break;
            }
        }

        bool fromBeyondEnd = lines.Count == 0 && count > 0 && from > 1 && toBeyondEnd;
        return new SourceLineReadResult(lines, fromBeyondEnd, toBeyondEnd);
    }

    /// <summary>读取位置与字符缓冲。</summary>
    private sealed class Cursor(TextReader reader)
    {
        private readonly TextReader _reader = reader;
        private readonly char[] _buffer = new char[BufferSize];
        private int _index;
        private int _count;
        private bool _endedWithTerminator;
        private bool _trailingLineRead;

        /// <summary>读取一行，没有更多内容时返回假。</summary>
        public bool TryReadLine(out string text, out string newLine)
        {
            if (AtTrailingLine())
            {
                _trailingLineRead = true;
                text = string.Empty;
                newLine = string.Empty;
                return true;
            }

            StringBuilder content = new();
            while (true)
            {
                int current = Peek();
                if (current < 0)
                {
                    text = content.ToString();
                    newLine = string.Empty;
                    return content.Length > 0;
                }

                if (current == '\r')
                {
                    Consume();
                    if (Peek() == '\n')
                    {
                        Consume();
                        newLine = "\r\n";
                    }
                    else
                    {
                        newLine = "\r";
                    }

                    _endedWithTerminator = true;
                    text = content.ToString();
                    return true;
                }

                if (current == '\n')
                {
                    Consume();
                    _endedWithTerminator = true;
                    text = content.ToString();
                    newLine = "\n";
                    return true;
                }

                content.Append((char)Consume());
                _endedWithTerminator = false;
            }
        }

        /// <summary>跳过一行，没有更多内容时返回假。</summary>
        public bool TrySkipLine()
        {
            if (AtTrailingLine())
            {
                _trailingLineRead = true;
                return true;
            }

            bool any = false;
            while (true)
            {
                int current = Peek();
                if (current < 0)
                {
                    return any;
                }

                any = true;
                Consume();
                if (current == '\r')
                {
                    if (Peek() == '\n')
                    {
                        Consume();
                    }

                    _endedWithTerminator = true;
                    return true;
                }

                if (current == '\n')
                {
                    _endedWithTerminator = true;
                    return true;
                }

                _endedWithTerminator = false;
            }
        }

        /// <summary>当前位置是否还有终止符之后的空行。</summary>
        private bool AtTrailingLine() =>
            !_trailingLineRead && _endedWithTerminator && Peek() < 0;

        /// <summary>查看下一个字符，不移动读取位置。到达结尾时返回 -1。</summary>
        private int Peek()
        {
            if (_index < _count)
            {
                return _buffer[_index];
            }

            return Fill() ? _buffer[_index] : -1;
        }

        /// <summary>取下一个字符并移动读取位置。到达结尾时返回 -1。</summary>
        private int Consume()
        {
            int current = Peek();
            if (current < 0)
            {
                return -1;
            }

            _index++;
            return current;
        }

        /// <summary>从文本来源填充缓冲，到达结尾时返回假。</summary>
        private bool Fill()
        {
            _count = _reader.Read(_buffer, 0, _buffer.Length);
            _index = 0;
            return _count > 0;
        }
    }
}