using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.FlowFiles;

namespace Kuroe.Workflows.FlowAssembly;

/// <summary>运行期的流程模板集合。装配与文件存取交给 FlowAssembler 与 FlowStore，
/// 这里只持有已装配的模板并提供查询；导入合并后落盘。</summary>
public sealed class FlowService
{
    private readonly FlowStore _store;
    private readonly SettingsProvider _settings;
    private readonly Lock _gate = new();
    private FlowFile _source;
    private List<FlowDefinition> _flows;

    private FlowService(FlowStore store, SettingsProvider settings, FlowFile source, List<FlowDefinition> flows)
    {
        _store = store;
        _settings = settings;
        _source = source;
        _flows = flows;
    }

    /// <summary>从流程文件装配模板集合。节点库或任一条流程不合法时返回全部错误，装配失败。</summary>
    internal static ErrorOr<FlowService> Create(FlowStore store, SettingsProvider settings)
    {
        ErrorOr<AssembledFlows> loaded = FlowAssembler.Load(store);
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        return new FlowService(store, settings, loaded.Value.Source, [.. loaded.Value.Expanded]);
    }

    /// <summary>提交任务未指定流程时用的流程名，取自用户层；未设置时为内置流程。</summary>
    public FlowName DefaultName => _settings.Current.Runtime.DefaultFlow ?? DefaultFlows.Name;

    /// <summary>全部流程，按文件顺序。</summary>
    public IReadOnlyList<FlowDefinition> All()
    {
        lock (_gate)
        {
            return [.. _flows];
        }
    }

    /// <summary>按名取展开后的流程，不存在时返回错误。</summary>
    public ErrorOr<FlowDefinition> Find(FlowName name)
    {
        lock (_gate)
        {
            FlowDefinition? flow = _flows.Find(candidate => candidate.Name == name);

            return flow is null ? [FlowErrors.FlowNotFound(name.Value)] : flow;
        }
    }

    /// <summary>提交任务未指定流程时用的那条流程。</summary>
    public ErrorOr<FlowDefinition> Default() => Find(DefaultName);

    /// <summary>从文件导入流程：节点库按名合并，任一条不合法整体不生效；同名整条覆盖，成功后落盘。</summary>
    public ErrorOr<FlowImport> Import(string source)
    {
        ErrorOr<string> resolved = _store.Resolve(source);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        ErrorOr<AssembledFlows> incoming = FlowAssembler.Read(resolved.Value);
        if (incoming.IsError)
        {
            return incoming.ErrorsOrEmptyList;
        }

        FlowFile merged = new(
            MergeLibrary(_source.Nodes, incoming.Value.Source.Nodes),
            MergeFlows(_source.Flows, incoming.Value.Source.Flows));

        ErrorOr<AssembledFlows> assembled = FlowAssembler.Assemble(merged);
        if (assembled.IsError)
        {
            return assembled.ErrorsOrEmptyList;
        }

        lock (_gate)
        {
            List<string> notes =
            [
                .. incoming.Value.Source.Flows.Select(flow => merged.Flows.Any(candidate => candidate.Name == flow.Name)
                    ? $"覆盖流程 {flow.Name}"
                    : $"新增流程 {flow.Name}"),
                .. incoming.Value.Source.Nodes.Select(node => $"节点库 {node.Name} 已并入。"),
            ];

            ErrorOr<Success> saved = FlowAssembler.Save(_store, merged);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _source = merged;
            _flows = [.. assembled.Value.Expanded];

            return new FlowImport(resolved.Value, [.. incoming.Value.Source.Flows.Select(flow => flow.Name)], notes);
        }
    }

    /// <summary>新库按名覆盖旧库、其余保留，顺序是旧在前新在后。</summary>
    private static List<NodeSpec> MergeLibrary(IReadOnlyList<NodeSpec> current, IReadOnlyList<NodeSpec> incoming)
    {
        Dictionary<NodeName, NodeSpec> merged = current.ToDictionary(node => node.Name);
        foreach (NodeSpec node in incoming)
        {
            merged[node.Name] = node;
        }

        return [.. merged.Values];
    }

    /// <summary>新流程按名覆盖旧流程、其余保留，顺序是旧在前新在后。</summary>
    private static List<FlowDefinition> MergeFlows(IReadOnlyList<FlowDefinition> current, IReadOnlyList<FlowDefinition> incoming)
    {
        Dictionary<FlowName, FlowDefinition> merged = current.ToDictionary(flow => flow.Name);
        foreach (FlowDefinition flow in incoming)
        {
            merged[flow.Name] = flow;
        }

        return [.. merged.Values];
    }
}
