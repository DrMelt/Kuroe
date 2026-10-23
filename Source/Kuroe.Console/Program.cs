using System.CommandLine;
using ErrorOr;
using Kuroe;
using Kuroe.Assistant;
using Kuroe.Console;
using Kuroe.Console.Commands;
using Kuroe.Console.Views;
using Kuroe.Configuration.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

RootCommand root = new("Kuroe 助手：终端 REPL，流式输出与自动工具调用");
Option<string?> workDirectory = new("--work-directory", "-d") { Description = "工作目录，未指定时用进程当前目录" };
root.Options.Add(workDirectory);
root.SetAction(parseResult => RunAsync(parseResult.GetValue(workDirectory)));

return await root.Parse(args).InvokeAsync();

static async Task<int> RunAsync(string? workDirectory)
{
    Terminal terminal = Terminal.Create();
    ErrorView errors = new(terminal);

    // 任务浏览与逐级下钻要读键盘，没有输入环境时不再降级呈现
    if (!Terminal.Interactive)
    {
        errors.Report([Error.Failure("Terminal.NotInteractive", "需要可交互的终端，当前无法读键盘。")]);
        return 1;
    }

    ErrorOr<KuroePaths> paths = KuroePaths.Resolve(workDirectory);
    if (paths.IsError)
    {
        errors.Report(paths.ErrorsOrEmptyList);
        return 1;
    }

    // 生成产物里随附仓库源码与文档，导出目录由宿主给出
    string sourceDirectory = Path.Combine(AppContext.BaseDirectory, "source");

    var services = new ServiceCollection();
    services.AddSingleton(terminal);
    services.AddSingleton(errors);
    ErrorOr<KuroeComposition> composition = services.AddKuroeCore(
        paths.Value,
        new KuroeCoreOptions { RegisterNodeTypes = NodeTypes.Register });
    if (composition.IsError)
    {
        errors.Report(composition.ErrorsOrEmptyList);
        return 1;
    }

    ErrorOr<Success> assistant = services.AddKuroeAssistant(composition.Value, sourceDirectory);
    if (assistant.IsError)
    {
        errors.Report(assistant.ErrorsOrEmptyList);
        return 1;
    }

    // 呈现与通知
    services.AddSingleton<ResultView>();
    services.AddSingleton<DialogueSink>();
    services.AddSingleton<ExecutionNotifier>();
    services.AddSingleton<TaskListView>();
    services.AddSingleton<TaskOverviewView>();
    services.AddSingleton<TaskTreeView>();
    services.AddSingleton<ExecutionDetailView>();
    services.AddSingleton<CatalogView>();
    services.AddSingleton<StartupView>();
    services.AddSingleton<TaskBrowser>();

    // 命令族与对话循环
    services.AddSingleton<SettingsCommands>();
    services.AddSingleton<ProviderCommands>();
    services.AddSingleton<ModelCommands>();
    services.AddSingleton<CatalogCommands>();
    services.AddSingleton<FlowCommands>();
    services.AddSingleton<ToolCommands>();
    services.AddSingleton<TaskCommands>();
    services.AddSingleton<ReplCommands>();
    services.AddSingleton<Repl>();

    // 日志全部导向 stderr，避免与 stdout 上的流式回复交错
    services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    services.AddLogging(builder => builder
        // 日志级别只在启动时生效
        .SetMinimumLevel(composition.Value.Startup.LogLevel)
        .AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        }));

    using var provider = services.BuildServiceProvider(
        new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    await provider.GetRequiredService<Repl>().RunAsync();

    return 0;
}
