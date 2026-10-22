using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Spectre.Console.Testing;

namespace Kuroe.Cli.Tests;

/// <summary>一套与库装配对应的命令对象和两个方向独立的终端捕获。</summary>
internal sealed class CliSession : IDisposable
{
    public CliSession(bool seedCatalog = true, string? flowsJson = null)
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

    public ProviderCommands Providers => new(Environment.Catalog, Printer, Terminal, Results);

    public ModelCommands Models => new(Environment.Catalog, Environment.Models, Printer, Terminal, Results, Errors);

    public CatalogCommands Catalogs => new(Environment.Catalog, Environment.Models, Printer, Terminal, Results);

    public FlowCommands Flows => new(Environment.Flows, Terminal, Results);

    public SettingsCommands Settings => new(Environment.Settings, Terminal, Results);

    public TaskCommands Tasks
    {
        get
        {
            TaskListView list = new(Terminal, Environment.Pendings);
            TaskOverviewView overview = new(Terminal, Environment.Pendings);
            TaskTreeView tree = new(Terminal, Environment.Pendings);
            ExecutionDetailView executionView = new(Terminal);
            TaskBrowser browser = new(Environment.Registry, Environment.Tasks, Environment.Pendings, list, overview, tree, executionView, Terminal, Results);

            return new TaskCommands(Environment.Registry, Environment.Tasks, Environment.Pendings, list, overview, tree, executionView, browser, Terminal, Results);
        }
    }

    public ReplCommands Repl
    {
        get
        {
            ProviderCommands providers = Providers;
            ModelCommands models = Models;
            CatalogCommands catalogs = Catalogs;
            SettingsCommands settings = Settings;
            TaskCommands tasks = Tasks;
            FlowCommands flows = Flows;
            ToolCommands tools = new(Environment.ConfiguredTools, Terminal);

            return new ReplCommands(Environment.Dialogue, Environment.Models, providers, models, catalogs,
                settings, tasks, flows, tools, Terminal, Errors);
        }
    }

    public void Dispose() => Environment.Dispose();
}
