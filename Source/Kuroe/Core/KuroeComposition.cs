using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool;
using Kuroe.Tool.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe;

/// <summary>一次装配的产物：启动信息与可继续并入的工具集。宿主完成全部装配后再构建容器。</summary>
public sealed class KuroeComposition
{
    private readonly IServiceCollection _services;
    private readonly List<IToolSource> _sources = [];

    internal KuroeComposition(
        IServiceCollection services,
        IReadOnlyList<ConfiguredTool> configuredTools,
        KuroeStartup startup)
    {
        _services = services;
        ConfiguredTools = configuredTools;
        Startup = startup;
    }

    /// <summary>启动信息，其中的日志级别用于宿主注册日志输出。</summary>
    public KuroeStartup Startup { get; }

    /// <summary>已装载的配置命令工具，供状态查询工具呈现。</summary>
    public IReadOnlyList<ConfiguredTool> ConfiguredTools { get; }

    /// <summary>把工具源并入工具集并注册，按函数路径与模型调用名校验重复。失败时不注册任何源。</summary>
    public ErrorOr<Success> AddToolSources(IEnumerable<IToolSource> sources)
    {
        List<IToolSource> incoming = [.. sources];
        List<ToolDefinition> definitions = [.. _sources.Concat(incoming).SelectMany(source => source.Definitions)];

        List<ToolPath> duplicates = [.. definitions
            .GroupBy(definition => definition.Path)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Count > 0)
        {
            return duplicates.Select(path => ToolFileErrors.Duplicate(path.Value)).ToList();
        }

        List<Error> conflicts = [.. definitions
            .GroupBy(definition => definition.Path.CallName.Value)
            .Where(group => group.Select(definition => definition.Path.Value).Distinct().Count() > 1)
            .Select(group => ToolFileErrors.CallNameConflict(
                group.Key, [.. group.Select(definition => definition.Path.Value).Distinct()]))];
        if (conflicts.Count > 0)
        {
            return conflicts;
        }

        foreach (IToolSource source in incoming)
        {
            _services.AddSingleton<IToolSource>(source);
            _sources.Add(source);
        }

        return Result.Success;
    }
}