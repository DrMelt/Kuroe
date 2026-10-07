using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>运行期的流程模板集合。加载时先校验节点库，再把每条装配流程展开成具体树并校验，
/// 全部非法项一次给出；导入合并后落盘。装配版保留引用形态，展开版供运行与展示。</summary>
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

    /// <summary>从流程文件装配。节点库或任一条流程不合法时返回全部错误，装配失败。</summary>
    internal static ErrorOr<FlowService> Create(FlowStore store, SettingsProvider settings)
    {
        ErrorOr<FlowFile> loaded = store.Load();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        List<Error> errors = Validate(loaded.Value, out List<FlowDefinition> expanded);
        if (FlowRules.Duplicated(expanded) is { } duplicated)
        {
            errors.Add(FlowErrors.Body(duplicated.Value, "流程名重复。"));
        }

        return errors.Count > 0 ? errors : new FlowService(store, settings, loaded.Value, expanded);
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

        ErrorOr<FlowFile> parsed = FlowStore.Read(resolved.Value);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        FlowFile incoming = parsed.Value;
        if (FlowRules.Duplicated(incoming.Flows) is { } duplicated)
        {
            return [FlowErrors.Body(duplicated.Value, "流程名重复。")];
        }

        FlowFile merged = new(
            MergeLibrary(_source.Nodes, incoming.Nodes),
            MergeFlows(_source.Flows, incoming.Flows));

        List<Error> errors = Validate(merged, out List<FlowDefinition> expanded);
        if (errors.Count > 0)
        {
            return errors;
        }

        lock (_gate)
        {
            List<string> notes =
            [
                .. incoming.Flows.Select(flow => merged.Flows.Any(candidate => candidate.Name == flow.Name)
                    ? $"覆盖流程 {flow.Name}"
                    : $"新增流程 {flow.Name}"),
                .. incoming.Nodes.Select(node => $"节点库 {node.Name} 已并入。"),
            ];

            ErrorOr<Success> saved = _store.Save(merged);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _source = merged;
            _flows = expanded;

            return new FlowImport(resolved.Value, [.. incoming.Flows.Select(flow => flow.Name)], notes);
        }
    }

    /// <summary>校验节点库并逐流程展开校验，合法流程经 valid 交回，返回值是全部错误。</summary>
    private static List<Error> Validate(FlowFile file, out List<FlowDefinition> valid)
    {
        List<Error> errors = [];
        ErrorOr<Success> libraryChecked = NodeLibraryRules.Validate(file.Nodes);
        if (libraryChecked.IsError)
        {
            errors.AddRange(libraryChecked.ErrorsOrEmptyList);
            valid = [];
            return errors;
        }

        var library = new Dictionary<NodeName, NodeSpec>();
        foreach (NodeSpec node in file.Nodes)
        {
            library.TryAdd(node.Name, node);
        }

        List<FlowDefinition> accepted = [];
        foreach (FlowDefinition flow in file.Flows)
        {
            ErrorOr<NodeSpec> expanded = NodeExpander.Expand(library, flow.RootNode, flow.Name.Value);
            if (expanded.IsError)
            {
                errors.AddRange(expanded.ErrorsOrEmptyList);
                continue;
            }

            FlowDefinition concrete = new(flow.Name, flow.Description, flow.Models, expanded.Value);
            ErrorOr<Success> checkedFlow = FlowRules.Validate(concrete);
            if (checkedFlow.IsError)
            {
                errors.AddRange(checkedFlow.ErrorsOrEmptyList);
            }
            else
            {
                accepted.Add(concrete);
            }
        }

        valid = accepted;

        return errors;
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
