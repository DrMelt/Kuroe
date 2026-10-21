using ErrorOr;
using Kuroe.Flow.Json;
using Kuroe.Flow.Json.Files;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.StaticGraph;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Workflows.Flows.Files;

namespace Kuroe.Workflows.Flows;

/// <summary>运行期的流程模板集合。文件形状的读写交给 Json 扩展，装配交给 FlowAssembler，落盘交给 FlowStore，
/// 这里持有基线节点库与已装配的模板并提供查询；导入合并后落盘。</summary>
public sealed class FlowService : IFlowCatalog
{
    private readonly FlowStore _store;
    private readonly NodeLibrary _builtins;
    private readonly NodeTypeFileRegistry _typeFiles;
    private readonly Lock _gate = new();
    private NodeLibrary _library;
    private List<NodeSpec> _fileNodes;
    private List<FlowDefinition> _flowModels;
    private List<FlowDefinition> _flows;

    private FlowService(
        FlowStore store,
        NodeLibrary builtins,
        NodeTypeFileRegistry typeFiles,
        AssembledFlows assembled)
    {
        _store = store;
        _builtins = builtins;
        _typeFiles = typeFiles;
        _library = assembled.Library;
        _fileNodes = [.. assembled.FileNodes];
        _flowModels = [.. assembled.FlowModels];
        _flows = [.. assembled.Flows];
    }

    /// <summary>从流程文件装配模板集合。内置节点装进基线库，节点库或任一条流程不合法时返回全部错误，装配失败。</summary>
    internal static ErrorOr<FlowService> Create(
        FlowStore store,
        IReadOnlyList<NodeSpec> builtinNodes,
        NodeTypeFileRegistry typeFiles)
    {
        NodeLibrary builtins = new();
        builtins.Register(builtinNodes);

        ErrorOr<FlowFileDto?> loaded = store.LoadDto();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        ErrorOr<AssembledFlows> assembled = FlowFile.Assemble(loaded.Value ?? new FlowFileDto(), builtins, typeFiles);
        if (assembled.IsError)
        {
            return assembled.ErrorsOrEmptyList;
        }

        return new FlowService(store, builtins, typeFiles, assembled.Value);
    }

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

    /// <summary>从文件导入流程：导入的文件要能独立装配；节点定义按名合并，任一条不合法整体不生效；同名整条覆盖，成功后落盘。</summary>
    public ErrorOr<FlowImport> Import(string source)
    {
        ErrorOr<string> resolved = _store.Resolve(source);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        ErrorOr<FlowFileDto> file = FlowStore.ReadDto(resolved.Value);
        if (file.IsError)
        {
            return file.ErrorsOrEmptyList;
        }

        ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> read = FlowFile.Read(file.Value, _typeFiles);
        if (read.IsError)
        {
            return read.ErrorsOrEmptyList;
        }

        ErrorOr<AssembledFlows> checkedStandalone = FlowAssembler.Assemble(read.Value.Nodes, read.Value.Flows, _builtins);
        if (checkedStandalone.IsError)
        {
            List<Error> errors = [.. checkedStandalone.ErrorsOrEmptyList, FlowErrors.ImportStandalone()];

            return errors;
        }

        lock (_gate)
        {
            // 独立装配已通过校验，文件节点在这里都是定义条目
            List<NodeSpec> fileNodes = MergeByName(_fileNodes, read.Value.Nodes, node => node.DefinitionName.Value);
            List<FlowDefinition> flowModels = MergeByName(_flowModels, read.Value.Flows, flow => flow.Name);

            ErrorOr<AssembledFlows> assembled = FlowAssembler.Assemble(fileNodes, flowModels, _library);
            if (assembled.IsError)
            {
                return assembled.ErrorsOrEmptyList;
            }

            List<string> notes =
            [
                .. read.Value.Flows.Select(flow => _flowModels.Any(existing => existing.Name == flow.Name)
                    ? $"覆盖流程 {flow.Name}"
                    : $"新增流程 {flow.Name}"),
                .. read.Value.Nodes.Select(node => $"节点库 {node.DefinitionName} 已并入。"),
            ];

            ErrorOr<Success> saved = _store.Save(FlowFile.Write(fileNodes, flowModels, _typeFiles));
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _library = assembled.Value.Library;
            _fileNodes = fileNodes;
            _flowModels = flowModels;
            _flows = [.. assembled.Value.Flows];

            return new FlowImport(resolved.Value, [.. read.Value.Flows.Select(flow => flow.Name)], notes);
        }
    }

    /// <summary>按名覆盖合并：新项覆盖旧项、其余保留，顺序是旧在前新在后。</summary>
    private static List<T> MergeByName<T, TKey>(IReadOnlyList<T> current, IReadOnlyList<T> incoming, Func<T, TKey> key)
        where TKey : notnull
    {
        Dictionary<TKey, T> merged = current.ToDictionary(key);
        foreach (T item in incoming)
        {
            merged[key(item)] = item;
        }

        return [.. merged.Values];
    }
}
