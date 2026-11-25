using ErrorOr;
using Kuroe.Composition.Configuration.Files;
using Kuroe.Kernel.Control.Runtime;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Input.Shared;
using Kuroe.Platform;
using Kuroe.Files.Shared;
using Kuroe.Ability.Tool;
using Kuroe.Ability.Tool.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Composition;

/// <summary>一次装配的产物：启动信息与可继续并入的工具集。宿主完成全部装配后再构建容器。</summary>
public sealed class KuroeComposition
{
    private readonly IServiceCollection _services;
    private readonly List<IToolSource> _sources = [];
    private readonly List<ToolDefinition> _definitions = [];

    private IExecutionRunnerFactory? _llmRunner;

    internal KuroeComposition(
        IServiceCollection services,
        IReadOnlyList<ConfiguredTool> configuredTools,
        KuroeStartup startup,
        IWorkspaceFiles files,
        KuroePaths paths,
        bool persistTasks,
        IInputChannel inputChannel,
        FlowTemplateCatalog flowTemplates,
        TimeProvider time)
    {
        _services = services;
        ConfiguredTools = configuredTools;
        Startup = startup;
        Files = files;
        Paths = paths;
        PersistTasks = persistTasks;
        InputChannel = inputChannel;
        FlowTemplates = flowTemplates;
        Time = time;
    }

    /// <summary>启动信息，其中的日志级别用于宿主注册日志输出。</summary>
    public KuroeStartup Startup { get; }

    /// <summary>本次装配使用的文件访问实现，后续并入的工具源共用它。</summary>
    public IWorkspaceFiles Files { get; }

    /// <summary>本次装配的工作目录路径。</summary>
    public KuroePaths Paths { get; }

    /// <summary>任务是否写存档并占用租约。</summary>
    public bool PersistTasks { get; }

    /// <summary>本次装配使用的输入登记，宿主与运行器共用同一实例。</summary>
    public IInputChannel InputChannel { get; }

    /// <summary>流程模板目录，流程实现装配时登记来源。</summary>
    public FlowTemplateCatalog FlowTemplates { get; }

    /// <summary>本次装配使用的时钟，容器注册与后续流程装配共用同一实例。</summary>
    public TimeProvider Time { get; }

    /// <summary>全部工具源，装配期加入后由工具工厂共用。</summary>
    public IReadOnlyList<IToolSource> ToolSources => _sources;

    /// <summary>Llm 执行运行器工厂。</summary>
    public IExecutionRunnerFactory LlmRunner =>
        _llmRunner ?? throw new InvalidOperationException("装配尚未建立 Llm 执行运行器工厂。");

    /// <summary>已装载的配置命令工具。</summary>
    public IReadOnlyList<ConfiguredTool> ConfiguredTools { get; }

    /// <summary>记下 Llm 执行运行器工厂，只在装配内调用。</summary>
    internal void AttachLlmRunner(IExecutionRunnerFactory runner) => _llmRunner = runner;

    /// <summary>已并入的工具定义。</summary>
    public IReadOnlyList<ToolDefinition> ToolDefinitions => _definitions;

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
            _services.AddSingleton(source);
            _sources.Add(source);
        }

        return Result.Success;
    }

    /// <summary>并入工具定义并按函数路径与模型调用名校验重复。</summary>
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