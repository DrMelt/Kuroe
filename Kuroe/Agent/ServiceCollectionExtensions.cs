using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddSingleton(collection.Value);
        services.AddSingleton<AgentClientProvider>();
        services.AddSingleton<AgentSession>();

        return Result.Success;
    }
}
