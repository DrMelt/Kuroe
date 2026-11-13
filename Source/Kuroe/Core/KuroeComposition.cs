using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool;
using Kuroe.Tool.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe;

/// <summary>一次装配的产物：启动信息与可继续并入的工具集。宿主完成全部装配后再构建容器。</summary>
public sealed class KuroeComposition
{
    private readonly IServiceCollection _services;
    private readonly List<IToolSource> _sources = [];
    private readonly List<ToolDefinition> _definitions = [];

    internal KuroeComposition(
        IServiceCollection services,
        IReadOnlyList<ConfiguredTool> configuredTools,
        KuroeStartup startup,
        IWorkspaceFiles files)
    {
        _services = services;
        ConfiguredTools = configuredTools;
        Startup = startup;
        Files = files;
    }

    /// <summary>启动信息，其中的日志级别用于宿主注册日志输出。</summary>
    public KuroeStartup Startup { get; }

    /// <summary>本次装配使用的文件访问实现，后续并入的工具源共用它。</summary>
    public IWorkspaceFiles Files { get; }

    /// <summary>已装载的配置命令工具，供状态查询工具呈现。</summary>
    public IReadOnlyList<ConfiguredTool> ConfiguredTools { get; }

    /// <summary>把工具源并入工具集并注册，按函数路径与模型调用名校验重复。失败时不注册任何源。</summary>
    public ErrorOr<Success> AddToolSources(IEnumerable<IToolSource> sources)
    {
        List<IToolSource> incoming = [.. sources];
        ErrorOr<Success> added = AddToolDefinitions(incoming.SelectMany(source => source.Definitions));
        if (added.IsError)
        {
            return added;
        }

        foreach (IToolSource source in incoming)
        {
            _services.AddSingleton<IToolSource>(source);
            _sources.Add(source);
        }

        return Result.Success;
    }

    /// <summary>并入工具定义并按函数路径与模型调用名校验重复。供由容器延迟创建的工具源预先占位。</summary>
    public ErrorOr<Success> AddToolDefinitions(IEnumerable<ToolDefinition> definitions)
    {
        List<ToolDefinition> incoming = [.. definitions];
        List<ToolDefinition> all = [.. _definitions, .. incoming];

        List<ToolPath> duplicates = [.. all
            .GroupBy(definition => definition.Path)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Count > 0)
        {
            return duplicates.Select(path => ToolFileErrors.Duplicate(path.Value)).ToList();
        }

        List<Error> conflicts = [.. all
            .GroupBy(definition => definition.Path.CallName.Value)
            .Where(group => group.Select(definition => definition.Path.Value).Distinct().Count() > 1)
            .Select(group => ToolFileErrors.CallNameConflict(
                group.Key, [.. group.Select(definition => definition.Path.Value).Distinct()]))];
        if (conflicts.Count > 0)
        {
            return conflicts;
        }

        _definitions.AddRange(incoming);

        return Result.Success;
    }
}