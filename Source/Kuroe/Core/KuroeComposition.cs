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
    private readonly List<ITool> _tools = [];

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

    /// <summary>把工具载体并入工具集并注册，按工具路径与模型调用名校验重复。失败时不注册任何载体。</summary>
    public ErrorOr<Success> AddTools(IEnumerable<ITool> tools)
    {
        List<ITool> incoming = [.. tools];
        List<ToolFunction> functions = [.. _tools.Concat(incoming).SelectMany(carrier => carrier.Functions)];

        List<ToolPath> duplicates = [.. functions
            .GroupBy(function => function.Definition.Path)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Count > 0)
        {
            return duplicates.Select(path => ToolFileErrors.Duplicate(path.Value)).ToList();
        }

        List<Error> conflicts = [.. functions
            .GroupBy(function => function.Definition.Path.CallName.Value)
            .Where(group => group.Select(function => function.Definition.Path.Value).Distinct().Count() > 1)
            .Select(group => ToolFileErrors.CallNameConflict(
                group.Key, [.. group.Select(function => function.Definition.Path.Value).Distinct()]))];
        if (conflicts.Count > 0)
        {
            return conflicts;
        }

        foreach (ITool tool in incoming)
        {
            _services.AddSingleton(tool);
            _tools.Add(tool);
        }

        return Result.Success;
    }
}