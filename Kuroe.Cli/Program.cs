using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

KuroePaths paths = KuroePaths.Resolve();

ErrorOr<CatalogService> catalog = CatalogService.Create(new CatalogStore(paths.CatalogFile));
if (catalog.IsError)
{
    Errors.Report(catalog.ErrorsOrEmptyList);
    return 1;
}

ErrorOr<SettingsProvider> settings = SettingsProvider.Create(paths);
if (settings.IsError)
{
    Errors.Report(settings.ErrorsOrEmptyList);
    return 1;
}

var services = new ServiceCollection();
ErrorOr<Success> agent = services.AddKuroeAgent(settings.Value, catalog.Value);
if (agent.IsError)
{
    Errors.Report(agent.ErrorsOrEmptyList);
    return 1;
}

// 日志全部导向 stderr，避免与 stdout 上的流式回复交错
services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
services.AddLogging(builder => builder
    // 日志级别只在启动时生效
    .SetMinimumLevel(settings.Value.Current.Agent.LogLevel)
    .AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    }));

using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

await Repl.RunAsync(
    provider.GetRequiredService<AgentSession>(),
    settings.Value,
    catalog.Value);

return 0;
