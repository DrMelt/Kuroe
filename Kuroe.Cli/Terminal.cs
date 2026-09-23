using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli;

/// <summary>终端输出的写出口与样式表。动态文本一律按纯文本写出，不进入 markup 解析。
/// 全部写入串行，避免并发 agent 的通知插进正在输出的行。</summary>
internal sealed class Terminal(IAnsiConsole output, IAnsiConsole error)
{
    private readonly Lock _gate = new();
    private readonly List<(string Text, Style Style)> _pending = [];
    private readonly AnsiWriter outputWriter = new(output.Profile.Out.Writer, output.Profile.Capabilities);
    private readonly AnsiWriter errorWriter = new(error.Profile.Out.Writer, error.Profile.Capabilities);
    private bool _exclusive;

    /// <summary>能力探测交给 Spectre，非终端与不支持 ANSI 的环境自动降级为无样式文本。</summary>
    public static Terminal Create() => new(
        AnsiConsole.Console,
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) }));

    /// <summary>能否读键盘。浏览与下钻要求交互终端。</summary>
    public static bool Interactive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected && AnsiConsole.Console.Profile.Capabilities.Interactive;

    /// <summary>写出参与排版的渲染元素。</summary>
    public void Write(IRenderable renderable)
    {
        lock (_gate)
        {
            output.Write(renderable);
        }
    }

    public void Line(string text) => Write(text, Style.Plain);

    public void Ok(string text) => Write(text, Styles.Success);

    public void Warn(string text) => Write(text, Styles.Warning);

    public void Hint(string text) => Write(text, Styles.Hint);

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
            outputWriter.Write(text, Style.Plain);
        }
    }

    public void NewLine()
    {
        lock (_gate)
        {
            outputWriter.WriteLine();
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

    /// <summary>建栅格，列间两个空格，wrapColumns 之外的列不换行。单元格会补空格到列宽。</summary>
    public static Grid Columns(int count, params int[] wrapColumns)
    {
        Grid grid = new();
        for (int i = 0; i < count; i++)
        {
            grid.AddColumn(wrapColumns.Contains(i) ? new GridColumn() : new GridColumn().NoWrap());
        }

        return grid;
    }

    private void Write(string text, Style style)
    {
        lock (_gate)
        {
            outputWriter.WriteLine(text, style);
        }
    }

    private void WriteError(string text, Style style)
    {
        lock (_gate)
        {
            errorWriter.WriteLine(text, style);
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

    /// <summary>用法、指引与次要信息。</summary>
    public static readonly Style Hint = new(Color.Grey);
}

