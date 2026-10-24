using Kuroe.Console.Views;
using Spectre.Console.Testing;

namespace Kuroe.Console.Tests;

/// <summary>一套与库装配对应的交互控制器和两个方向独立的终端捕获。</summary>
internal sealed class ConsoleSession : IDisposable
{
    public ConsoleSession(bool seedCatalog = true, string? flowsJson = null)
    {
        Environment = TestEnvironment.Create(flowsJson, seedCatalog: seedCatalog);
        Terminal = NewTerminal(out TestConsole output, out TestConsole errorsOut);
        Output = output;
        ErrorsOut = errorsOut;
        Errors = new ErrorView(Terminal);
        Results = new ResultView(Terminal, Errors);
        Printer = new CatalogView(Terminal);
    }

    public TestEnvironment Environment { get; }

    public TestConsole Output { get; }

    public TestConsole ErrorsOut { get; }

    public Terminal Terminal { get; }

    public ErrorView Errors { get; }

    public ResultView Results { get; }

    public CatalogView Printer { get; }

    public TaskOverviewView Overview => new(Terminal, Environment.Pendings);

    public ExecutionDetailView ExecutionDetail => new(Terminal);

    public TaskBrowser Tasks
    {
        get
        {
            TaskListView list = new(Terminal, Environment.Pendings);
            TaskOverviewView overview = new(Terminal, Environment.Pendings);
            TaskTreeView tree = new(Terminal, Environment.Pendings);
            ExecutionDetailView execution = new(Terminal);

            return new TaskBrowser(Environment.Registry, Environment.Tasks, Environment.Pendings, Environment.Flows,
                list, overview, tree, execution, Terminal, Results);
        }
    }

    public FlowBrowser Flows => new(Environment.Flows, Terminal, Results);

    public CatalogBrowser Catalogs =>
        new(Environment.Catalog, Environment.Models, Printer, Terminal, Results, Errors);

    public SettingsBrowser Settings => new(Environment.Settings, Terminal, Results);

    public ToolView Tools => new(Environment.Tools, Environment.ConfiguredTools, Terminal);

    public DialogueBrowser Assistant =>
        new(Environment.Dialogue, Environment.Models, Catalogs, Environment.Settings, new DialogueSink(Terminal), Terminal, Errors);

    public Repl Repl => new(
        Environment.Tasks,
        Assistant,
        new ExecutionNotifier(Environment.Registry, Terminal),
        new StartupView(Environment.Catalog, Environment.Models, Environment.Tools, Environment.Flows, Terminal),
        Tasks,
        Flows,
        Catalogs,
        Settings,
        Tools,
        Terminal);

    /// <summary>建一套关闭 ANSI 的测试终端，输出里不含颜色序列，按纯文本断言。</summary>
    public static Terminal NewTerminal(out TestConsole output, out TestConsole error)
    {
        TestConsole stdout = NewConsole();
        TestConsole stderr = NewConsole();
        output = stdout;
        error = stderr;

        return new Terminal(stdout, stderr);
    }

    private static TestConsole NewConsole()
    {
        TestConsole console = new();
        console.Profile.Capabilities.Ansi = false;

        return console;
    }

    public void Dispose() => Environment.Dispose();
}
