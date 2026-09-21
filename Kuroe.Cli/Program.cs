using ErrorOr;
using Kuroe;
using Kuroe.Cli;
using Kuroe.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

KuroePaths paths = KuroePaths.Resolve();

var services = new ServiceCollection();
ErrorOr<KuroeStartup> startup = services.AddKuroe(paths);
if (startup.IsError)
{
    Errors.Report(startup.ErrorsOrEmptyList);
    return 1;
}

services.AddSingleton<Repl>();
services.AddSingleton<ReplCommands>();

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
