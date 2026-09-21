using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Kuroe.Agent;

/// <summary>智能体的注册表，新增工具或服务改这里。</summary>
public static class ServiceCollectionExtensions
{
    public static ErrorOr<Success> AddKuroeAgent(
        this IServiceCollection services,
        SettingsProvider settings,
        CatalogService catalog)
    {
        IAgentTool[] providers = [new TimeTool(), new WeatherTool()];
        ErrorOr<IReadOnlyList<AITool>> collection = ToolCollection.Create(providers);
        if (collection.IsError)
        {
            return collection.ErrorsOrEmptyList;
        }

        services.AddSingleton(settings);
        services.AddSingleton(catalog);

        // 日志全部导向 stderr，避免与 stdout 上的流式回复交错
        services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        services.AddLogging(builder => builder
            // 日志级别只在启动时生效
            .SetMinimumLevel(settings.Current.Agent.LogLevel)
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            }));

        services.AddSingleton(collection.Value);
        services.AddSingleton<AgentClientProvider>();
        services.AddSingleton<AgentSession>();

        return Result.Success;
    }
}
