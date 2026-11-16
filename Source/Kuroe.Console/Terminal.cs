using Kuroe.Console.Views;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console;

/// <summary>终端输出的写出口与样式表。动态文本一律按纯文本写出，不进入 markup 解析。
/// 全部写入串行，避免并发执行的通知插进正在输出的行。</summary>
internal sealed class Terminal(IAnsiConsole output, IAnsiConsole error)
{
    private readonly Lock _gate = new();
    private readonly List<(string Text, Style Style)> _pending = [];
    private readonly AnsiWriter outputWriter = new(output.Profile.Out.Writer, output.Profile.Capabilities);
    private readonly AnsiWriter errorWriter = new(error.Profile.Out.Writer, error.Profile.Capabilities);
    private bool _exclusive;
    private bool _pageShown;

    /// <summary>能力探测交给 Spectre，非终端与不支持 ANSI 的环境自动降级为无样式文本。</summary>
    public static Terminal Create() => new(
        AnsiConsole.Console,
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(System.Console.Error) }));

    /// <summary>能否读键盘。浏览与下钻要求交互终端。</summary>
    public static bool Interactive =>
        !System.Console.IsInputRedirected && !System.Console.IsOutputRedirected && AnsiConsole.Console.Profile.Capabilities.Interactive;

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

    /// <summary>页面层级标题：路径段按 › 连接，标题按纯文本处理并加粗，横线与标题用强调色，横线占满终端宽度。层级之间由它划分，页内内容由框承载。
    /// 写页面标题即重绘页面：首个页面保留已有输出，其后先清理屏幕再写标题，标题从首行开始。</summary>
    public void Page(params string[] path)
    {
        lock (_gate)
        {
            if (_pageShown)
            {
                ClearScreen();
            }

            _pageShown = true;
            output.Write(new Rule($"[bold]{Markup.Escape(string.Join(" › ", path))}[/]")
            {
                Justification = Justify.Left,
                Style = Styles.Page,
            });
        }
    }

    /// <summary>清理可见屏幕与滚动区历史，光标回到左上角。写入器按输出能力决定是否写控制序列，不支持时页面按追加方式呈现。</summary>
    private void ClearScreen()
    {
        outputWriter.EraseInDisplay(2);
        outputWriter.ClearScrollback();
        outputWriter.CursorPosition(1, 1);
    }

    /// <summary>错误写 stderr，与 stdout 上的回复分流。</summary>
    public void Error(string text) => WriteError(text, Styles.Error);

    /// <summary>详情视图里的失败行。它是一次完整输出的一部分，因此与 <see cref="Error"/> 不同，走 stdout。</summary>
    public void Failure(string text) => Write(text, Styles.Error);

    /// <summary>随错误一起写 stderr 的收尾说明，与错误同流以免单独重定向时被拆散。</summary>
    public void Note(string text) => WriteError(text, Styles.Warning);

    /// <summary>结束当前行。</summary>
    public void NewLine()
    {
        lock (_gate)
        {
            outputWriter.WriteLine();
        }
    }

    /// <summary>一行次要提示。浏览与前台回复期间先排队，退出独占后冲刷。</summary>
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

    /// <summary>独占终端：读键的控件与回复期间不接受穿插输出。作用域结束时冲刷排队的通知。</summary>
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
    public T Prompt<T>(IPrompt<T> prompt) => output.Prompt(prompt);

    /// <summary>读键选择，Esc 返回取消项。调用方须持有独占呈现。</summary>
    public T Choose<T>(string title, IReadOnlyList<T> choices, Func<T, string> label, T cancel)
        where T : notnull => Prompt(new MenuPrompt<T>(title, choices, label, cancel));

    /// <summary>读一行文本，Esc 或空输入返回 null。调用方须持有独占呈现。</summary>
    public string? Ask(string label) => Prompt(new LinePrompt(label));

    /// <summary>读一行凭据，输入以掩码显示，Esc 或空输入返回 null。调用方须持有独占呈现。</summary>
    public string? AskSecret(string label) => Prompt(new LinePrompt(label, secret: true));

    /// <summary>读多行文本，回车换行，单独一行 ;; 提交，Esc 或空输入返回 null。调用方须持有独占呈现。</summary>
    public string? AskMultiline(string label) => Prompt(new MultilinePrompt(label));

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

    /// <summary>框线与框标题。</summary>
    public static readonly Style Border = new(Color.Grey);

    /// <summary>页面标题横线与标题。</summary>
    public static readonly Style Page = new(Color.Aqua);

    /// <summary>等待按键的框。</summary>
    public static readonly Style BorderActive = new(Color.Blue);

    /// <summary>用法、指引与次要信息。</summary>
    public static readonly Style Hint = new(Color.Grey);
}

