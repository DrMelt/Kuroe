using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli;

/// <summary>终端输出的写出口与样式表。动态文本一律按纯文本写出，不进入 markup 解析。</summary>
internal sealed class Terminal(IAnsiConsole output, IAnsiConsole error)
{
    private readonly AnsiWriter outputWriter = new(output.Profile.Out.Writer, output.Profile.Capabilities);
    private readonly AnsiWriter errorWriter = new(error.Profile.Out.Writer, error.Profile.Capabilities);

    /// <summary>能力探测交给 Spectre，非终端与不支持 ANSI 的环境自动降级为无样式文本。</summary>
    public static Terminal Create() => new(
        AnsiConsole.Console,
        AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) }));

    /// <summary>写出参与排版的渲染元素。</summary>
    public void Write(IRenderable renderable) => output.Write(renderable);

    public void Line(string text) => outputWriter.WriteLine(text, Style.Plain);

    public void Ok(string text) => outputWriter.WriteLine(text, Styles.Success);

    public void Warn(string text) => outputWriter.WriteLine(text, Styles.Warning);

    public void Hint(string text) => outputWriter.WriteLine(text, Styles.Hint);

    /// <summary>错误写 stderr，与 stdout 上的流式回复分流。</summary>
    public void Error(string text) => errorWriter.WriteLine(text, Styles.Error);

    /// <summary>随错误一起写 stderr 的收尾说明，与错误同流以免单独重定向时被拆散。</summary>
    public void Note(string text) => errorWriter.WriteLine(text, Styles.Warning);

    /// <summary>在当前行追加文本，不排版也不换行，用于提示符与流式增量。</summary>
    public void Append(string text) => outputWriter.Write(text, Style.Plain);

    public void NewLine() => outputWriter.WriteLine();

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
