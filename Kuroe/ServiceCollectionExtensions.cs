using ErrorOr;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe;

/// <summary>Kuroe 的装配入口，新增工具或服务改这里。宿主提供路径，日志输出由宿主注册。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>装配配置、目录、工具与会话，失败时返回启动期的全部错误。</summary>
    /// <returns>启动信息，其中的日志级别用于宿主注册日志输出。</returns>
    public static ErrorOr<KuroeStartup> AddKuroe(this IServiceCollection services, KuroePaths paths)
    {
        ErrorOr<SettingsProvider> settings = SettingsProvider.Create(paths);
        if (settings.IsError)
        {
            return settings.ErrorsOrEmptyList;
        }

        ErrorOr<CatalogService> catalog = CatalogService.Create(new CatalogStore(paths.CatalogFile));
        if (catalog.IsError)
        {
            return catalog.ErrorsOrEmptyList;
        }

        services.AddSingleton(settings.Value);
        services.AddSingleton(catalog.Value);
        services.AddSingleton<IAgentTool, TimeTool>();
        services.AddSingleton<IAgentTool, WeatherTool>();
        services.AddSingleton<ToolCollection>();
        services.AddSingleton<AgentClientProvider>();
        services.AddSingleton<AgentSession>();

        return new KuroeStartup(settings.Value.Current.Agent.LogLevel);
    }
}
