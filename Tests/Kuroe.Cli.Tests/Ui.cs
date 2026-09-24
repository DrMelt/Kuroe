using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.TestSupport;
using Spectre.Console.Testing;

namespace Kuroe.Cli.Tests;

/// <summary>一套与库装配对应的命令对象和两个方向独立的终端捕获。</summary>
internal sealed class Ui : IDisposable
{
    public Ui(bool seedCatalog = true)
    {
        Harness = KuroeHarness.Create(seedCatalog: seedCatalog);
        Terminal = NewTerminal(out TestConsole output, out TestConsole errorsOut);
        Output = output;
        ErrorsOut = errorsOut;
        Errors = new ErrorPrinter(Terminal);
        Results = new ResultPrinter(Terminal, Errors);
        Printer = new CatalogPrinter(Terminal);
    }

    public KuroeHarness Harness { get; }

    public TestConsole Output { get; }

    public TestConsole ErrorsOut { get; }

    public Terminal Terminal { get; }

    public ErrorPrinter Errors { get; }

    public ResultPrinter Results { get; }

    public CatalogPrinter Printer { get; }

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

    public ProviderCommands Providers => new(Harness.Catalog, Printer, Terminal, Results);

    public ModelCommands Models => new(Harness.Catalog, Harness.Models, Printer, Terminal, Results, Errors);

    public CatalogCommands Catalogs => new(Harness.Catalog, Printer, Terminal, Results);

    public FlowCommands Flows => new(Harness.Flows, Harness.Settings, Terminal, Results);

    public SettingsCommands Settings => new(Harness.Settings, Terminal, Results);

    public TaskCommands Tasks
    {
        get
        {
            TaskListView list = new(Terminal);
            TaskDetailView detail = new(Terminal);
            AgentDetailView agent = new(Terminal);
            TaskBrowser browser = new(Harness.Registry, Harness.Tasks, list, detail, agent, Terminal, Results);

            return new TaskCommands(Harness.Registry, Harness.Tasks, list, detail, agent, browser, Terminal, Results);
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

            return new ReplCommands(Harness.Registry, Harness.Models, providers, models, catalogs,
                settings, tasks, flows, Terminal, Errors);
        }
    }

    public void Dispose() => Harness.Dispose();
}