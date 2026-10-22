using System.Text;
using Kuroe.Cli.Views;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli;

/// <summary>终端输出的写出口与样式表。动态文本一律按纯文本写出，不进入 markup 解析。
/// 全部写入串行，避免并发执行的通知插进正在输出的行。</summary>
internal sealed class Terminal(IAnsiConsole output, IAnsiConsole error)
{
    private readonly Lock _gate = new();
    private readonly List<(string Text, Style Style)> _pending = [];
    private readonly AnsiWriter outputWriter = new(output.Profile.Out.Writer, output.Profile.Capabilities);
    private readonly AnsiWriter errorWriter = new(error.Profile.Out.Writer, error.Profile.Capabilities);
    private bool _exclusive;
    private Frame? _frame;

    /// <summary>当前终端宽度。窗口尺寸变化后重新读取。</summary>
    private int Width => output.Profile.Width;

    /// <summary>当前是否停在一行中间。行式输出前先闭合它。</summary>
    private bool _lineOpen;

    /// <summary>能力探测交给 Spectre，非终端与不支持 ANSI 的环境自动降级为无样式文本。</summary>
    public static Terminal Create() => new(
        AnsiConsole.Console,
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) }));

    /// <summary>能否读键盘。浏览与下钻要求交互终端。</summary>
    public static bool Interactive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected && AnsiConsole.Console.Profile.Capabilities.Interactive;

    /// <summary>写出参与排版的渲染元素。框内不接受整块排版。</summary>
    public void Write(IRenderable renderable)
    {
        lock (_gate)
        {
            if (_frame is not null)
            {
                throw new InvalidOperationException("框内只能写出文本行。");
            }

            CloseLine();
            output.Write(renderable);
        }
    }

    public void Line(string text) => Write(text, Style.Plain);

    public void Ok(string text) => Write(text, Styles.Success);

    public void Warn(string text) => Write(text, Styles.Warning);

    public void Hint(string text) => Write(text, Styles.Hint);

    /// <summary>页面层级标题：标题按纯文本处理，横线占满终端宽度。层级之间由它划分，页内内容由框承载。</summary>
    public void Page(string title) =>
        Write(new Rule(Markup.Escape(title))
        {
            Justification = Justify.Left,
            Style = Styles.Border,
        });

    /// <summary>工具调用行。</summary>
    public void ToolCall(string text) => Write(text, Styles.Key);

    /// <summary>工具结果行。</summary>
    public void ToolResult(string text) => Write(text, Styles.Hint);

    /// <summary>错误写 stderr，与 stdout 上的流式回复分流。</summary>
    public void Error(string text) => WriteError(text, Styles.Error);

    /// <summary>详情视图里的失败行。它是一次完整输出的一部分，因此与 <see cref="Error"/> 不同，走 stdout。</summary>
    public void Failure(string text) => Write(text, Styles.Error);

    /// <summary>随错误一起写 stderr 的收尾说明，与错误同流以免单独重定向时被拆散。</summary>
    public void Note(string text) => WriteError(text, Styles.Warning);

    /// <summary>在当前行追加文本，不排版也不换行，用于提示符与流式增量。</summary>
    public void Append(string text)
    {
        lock (_gate)
        {
            if (_frame is { } frame)
            {
                frame.Append(text, Style.Plain);

                return;
            }

            outputWriter.Write(text, Style.Plain);
            if (text.Length > 0)
            {
                _lineOpen = !text.EndsWith('\n');
            }
        }
    }

    /// <summary>结束当前行。</summary>
    public void NewLine()
    {
        lock (_gate)
        {
            if (_frame is { } frame)
            {
                frame.Blank();

                return;
            }

            outputWriter.WriteLine();
            _lineOpen = false;
        }
    }

    /// <summary>读一行输入，读到流末尾时为空。</summary>
    public static string? ReadLine() => Console.ReadLine();

    /// <summary>订阅 Ctrl+C。中断不结束进程，中断什么由处理器决定。</summary>
    public static void OnInterrupt(Action handler) => Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        handler();
    };

    /// <summary>一行次要提示。浏览与前台流式期间先排队，退出独占后冲刷。</summary>
    public void Notice(string text, Style style)
    {
        lock (_gate)
        {
            if (_exclusive)
            {
                _pending.Add((text, style));

                return;
            }
        }

        Write(text, style);
    }

    /// <summary>把排队中的通知写出去，只由独占作用域结束时调用。</summary>
    private void FlushNotices()
    {
        (string Text, Style Style)[] queued;
        lock (_gate)
        {
            if (_exclusive || _pending.Count == 0)
            {
                return;
            }

            queued = [.. _pending];
            _pending.Clear();
        }

        foreach ((string text, Style style) in queued)
        {
            Write(text, style);
        }
    }

    /// <summary>独占终端：读键的控件与流式回复期间不接受穿插输出。作用域结束时冲刷排队的通知。</summary>
    public IDisposable Exclusive()
    {
        lock (_gate)
        {
            if (_exclusive)
            {
                throw new InvalidOperationException("终端已被独占呈现。");
            }

            _exclusive = true;
        }

        return new Exclusivity(this);
    }

    /// <summary>读键控件用的出口。调用方须持有独占呈现。</summary>
    public T Prompt<T>(IPrompt<T> prompt)
        where T : notnull => output.Prompt(prompt);

    /// <summary>开一个文本框。回合结束后释放，底边由释放收尾。</summary>
    public Frame OpenFrame(BoxKind kind, string title)
    {
        lock (_gate)
        {
            if (_frame is not null)
            {
                throw new InvalidOperationException("终端已在框内。");
            }

            CloseLine();
            Frame frame = new(this, kind, title);
            _frame = frame;
            frame.Open();

            return frame;
        }
    }

    private void Write(string text, Style style)
    {
        lock (_gate)
        {
            if (_frame is { } frame)
            {
                frame.WriteLine(text, style);

                return;
            }

            CloseLine();
            outputWriter.WriteLine(text, style);
        }
    }

    private void WriteError(string text, Style style)
    {
        lock (_gate)
        {
            CloseLine();
            errorWriter.WriteLine(text, style);
        }
    }

    /// <summary>停在一行中间时换行：行式输出前先闭合它，流式输出后也由调用方收尾。框内由框自己收行。</summary>
    public void CloseLine()
    {
        if (_frame is not null)
        {
            return;
        }

        if (_lineOpen)
        {
            outputWriter.WriteLine();
            _lineOpen = false;
        }
    }

    /// <summary>文本流出的框：顶边、左右竖线、按终端宽度折行与底边。回合期间由它独占写出。
    /// 边框字符与样式取自皮肤，与面板保持同一份外观。</summary>
    internal sealed class Frame(Terminal terminal, BoxKind kind, string title) : IDisposable
    {
        private readonly Terminal _terminal = terminal;
        private readonly string _title = title;
        private readonly BoxSkin _skin = BoxSkin.Of(kind);
        private bool _bordered;
        private bool _lineOpen;
        private bool _opened;
        private bool _disposed;
        private int _width;
        private int _used;

        /// <summary>写顶边。终端过窄时不画框。</summary>
        internal void Open()
        {
            lock (_terminal._gate)
            {
                _width = _terminal.Width;
                _bordered = _width >= BoxSkin.MinWidth;
                _opened = _bordered;
                if (!_bordered)
                {
                    return;
                }

                string left = _skin.Shape.GetPart(BoxBorderPart.TopLeft);
                string top = _skin.Shape.GetPart(BoxBorderPart.Top);
                string right = _skin.Shape.GetPart(BoxBorderPart.TopRight);
                int fill = Math.Max(0, _width - left.GetCellWidth() - top.GetCellWidth()
                    - _title.GetCellWidth() - right.GetCellWidth());

                _terminal.outputWriter.Write(left, _skin.Line);
                _terminal.outputWriter.Write(top, _skin.Line);
                _terminal.outputWriter.Write(_title, _skin.Line);
                _terminal.outputWriter.Write(new string(top[0], fill), _skin.Line);
                _terminal.outputWriter.Write(right, _skin.Line);
                _terminal.outputWriter.WriteLine();
            }
        }

        /// <summary>追加一段文本。换行、制表符与控制字符在这里处理。</summary>
        internal void Append(string text, Style style)
        {
            lock (_terminal._gate)
            {
                foreach (Rune rune in text.EnumerateRunes())
                {
                    switch (rune.Value)
                    {
                        case '\r':
                            continue;
                        case '\n':
                            EnsureLine();
                            EndLine();
                            continue;
                        case '\t':
                            WriteText("    ", style);
                            continue;
                        default:
                            if (!Rune.IsControl(rune))
                            {
                                WriteText(rune.ToString(), style);
                            }

                            continue;
                    }
                }
            }
        }

        /// <summary>另起一行写入文本。文本内部的换行同样折行。</summary>
        internal void WriteLine(string text, Style style)
        {
            lock (_terminal._gate)
            {
                EndLine();
                Append(text, style);
                EnsureLine();
                EndLine();
            }
        }

        /// <summary>写一个空行。</summary>
        internal void Blank()
        {
            lock (_terminal._gate)
            {
                EnsureLine();
                EndLine();
            }
        }

        /// <summary>补齐当前行并写底边。</summary>
        public void Dispose()
        {
            lock (_terminal._gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                EndLine();

                _width = _terminal.Width;
                if (_opened)
                {
                    string left = _skin.Shape.GetPart(BoxBorderPart.BottomLeft);
                    string bottom = _skin.Shape.GetPart(BoxBorderPart.Bottom);
                    string right = _skin.Shape.GetPart(BoxBorderPart.BottomRight);
                    int fill = Math.Max(0, _width - left.GetCellWidth() - right.GetCellWidth());

                    _terminal.outputWriter.Write(left, _skin.Line);
                    _terminal.outputWriter.Write(new string(bottom[0], fill), _skin.Line);
                    _terminal.outputWriter.Write(right, _skin.Line);
                    _terminal.outputWriter.WriteLine();
                }

                _terminal._frame = null;
            }
        }

        /// <summary>当前行的内容宽度。左右边框与内边距之外都是内容。</summary>
        private int ContentWidth => Math.Max(1, _width
            - _skin.Shape.GetPart(BoxBorderPart.Left).GetCellWidth()
            - _skin.Shape.GetPart(BoxBorderPart.Right).GetCellWidth()
            - BoxSkin.Padding.Left - BoxSkin.Padding.Right);

        private void WriteText(string text, Style style)
        {
            int width = text.GetCellWidth();
            if (width <= 0)
            {
                EnsureLine();
                _terminal.outputWriter.Write(text, style);

                return;
            }

            if (_lineOpen && _used + width > ContentWidth)
            {
                EndLine();
            }

            EnsureLine();
            _terminal.outputWriter.Write(text, style);
            _used += width;
        }

        /// <summary>开一个内容行。宽度与是否带边框按当前终端宽度决定。</summary>
        private void EnsureLine()
        {
            if (_lineOpen)
            {
                return;
            }

            _width = _terminal.Width;
            _bordered = _width >= BoxSkin.MinWidth;
            if (_bordered)
            {
                _terminal.outputWriter.Write(_skin.Shape.GetPart(BoxBorderPart.Left), _skin.Line);
                _terminal.outputWriter.Write(new string(' ', BoxSkin.Padding.Left), Style.Plain);
            }

            _lineOpen = true;
            _used = 0;
        }

        /// <summary>闭合内容行：补满内容宽度与右边框，或直接换行。</summary>
        private void EndLine()
        {
            if (!_lineOpen)
            {
                return;
            }

            if (_bordered)
            {
                int width = ContentWidth;
                if (width > _used)
                {
                    _terminal.outputWriter.Write(new string(' ', width - _used), Style.Plain);
                }

                _terminal.outputWriter.Write(new string(' ', BoxSkin.Padding.Right), Style.Plain);
                _terminal.outputWriter.Write(_skin.Shape.GetPart(BoxBorderPart.Right), _skin.Line);
            }

            _terminal.outputWriter.WriteLine();
            _lineOpen = false;
            _used = 0;
        }
    }

    private sealed class Exclusivity(Terminal terminal) : IDisposable
    {
        public void Dispose()
        {
            lock (terminal._gate)
            {
                terminal._exclusive = false;
            }

            terminal.FlushNotices();
        }
    }
}

/// <summary>终端样式表。</summary>
internal static class Styles
{
    public static readonly Style Error = new(Color.Red);
    public static readonly Style Warning = new(Color.Yellow);
    public static readonly Style Success = new(Color.Green);

    /// <summary>栅格首列的命令名与键名。</summary>
    public static readonly Style Key = new(Color.Blue);

    /// <summary>框线与框标题。</summary>
    public static readonly Style Border = new(Color.Grey);

    /// <summary>等待按键的框。</summary>
    public static readonly Style BorderActive = new(Color.Blue);

    /// <summary>用法、指引与次要信息。</summary>
    public static readonly Style Hint = new(Color.Grey);
}

