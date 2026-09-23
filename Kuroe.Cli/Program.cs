using System.CommandLine;
using ErrorOr;
using Kuroe;
using Kuroe.Cli;
using Kuroe.Cli.Commands;
using Kuroe.Cli.Views;
using Kuroe.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

RootCommand root = new("Kuroe 智能体：终端 REPL，流式输出与自动工具调用");
Option<string?> workDirectory = new("--work-directory", "-d") { Description = "工作目录，未指定时用进程当前目录" };
root.Options.Add(workDirectory);
root.SetAction(parseResult => RunAsync(parseResult.GetValue(workDirectory)));

return await root.Parse(args).InvokeAsync();

static async Task<int> RunAsync(string? workDirectory)
{
    Terminal terminal = Terminal.Create();
    ErrorPrinter errors = new(terminal);

    // 任务浏览与逐级下钻要读键盘，没有输入环境时不再降级呈现
    if (!Terminal.Interactive)
    {
        errors.Report([Error.Failure("Terminal.NotInteractive", "需要可交互的终端，当前无法读键盘。")]);
        return 1;
    }

    ErrorOr<string> directory = WorkDirectory.Resolve(workDirectory);
    if (directory.IsError)
    {
        errors.Report(directory.ErrorsOrEmptyList);
        return 1;
    }

    KuroePaths paths = KuroePaths.At(directory.Value);

    var services = new ServiceCollection();
    services.AddSingleton(terminal);
    services.AddSingleton(errors);
    ErrorOr<KuroeStartup> startup = services.AddKuroe(paths);
    if (startup.IsError)
    {
        errors.Report(startup.ErrorsOrEmptyList);
        return 1;
    }

    // 呈现与通知
    services.AddSingleton<ResultPrinter>();
    services.AddSingleton<DialogueSink>();
    services.AddSingleton<RunNotifier>();
    services.AddSingleton<TaskListView>();
    services.AddSingleton<TaskDetailView>();
    services.AddSingleton<AgentDetailView>();
    services.AddSingleton<CatalogPrinter>();
    services.AddSingleton<StartupView>();
    services.AddSingleton<TaskBrowser>();

    // 命令族与对话循环
    services.AddSingleton<SettingsCommands>();
    services.AddSingleton<ProviderCommands>();
    services.AddSingleton<ModelCommands>();
    services.AddSingleton<CatalogCommands>();
    services.AddSingleton<FlowCommands>();
    services.AddSingleton<TaskCommands>();
    services.AddSingleton<ReplCommands>();
    services.AddSingleton<Repl>();

    // 日志全部导向 stderr，避免与 stdout 上的流式回复交错
    services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    services.AddLogging(builder => builder
        // 日志级别只在启动时生效
        .SetMinimumLevel(startup.Value.LogLevel)
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
