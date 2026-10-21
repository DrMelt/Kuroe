using ErrorOr;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Workflows.Flows.Files;

namespace Kuroe.Workflows.Flows;

/// <summary>运行期的流程模板集合。描述文档是授权来源，编译结果是运行期来源，落盘交给 FlowStore。</summary>
public sealed class FlowService : IFlowCatalog, IFlowRuntimeCatalog
{
    private readonly FlowStore _store;
    private readonly IReadOnlyList<NodeSpec> _builtins;
    private readonly IFlowDescriptionCompiler _compiler;
    private readonly Lock _gate = new();
    private FlowDocument _document;
    private CompiledDocument _compiled;

    private FlowService(
        FlowStore store,
        IReadOnlyList<NodeSpec> builtins,
        IFlowDescriptionCompiler compiler,
        FlowDocument document,
        CompiledDocument compiled)
    {
        _store = store;
        _builtins = builtins;
        _compiler = compiler;
        _document = document;
        _compiled = compiled;
    }

    /// <summary>从流程文件编译模板集合。文档或任一条流程不合法时返回全部错误。</summary>
    internal static ErrorOr<FlowService> Create(
        FlowStore store,
        IReadOnlyList<NodeSpec> builtinNodes,
        IFlowDescriptionCompiler compiler)
    {
        ErrorOr<FlowDocument?> loaded = store.Load();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        FlowDocument document = loaded.Value ?? new FlowDocument([], []);
        ErrorOr<CompiledDocument> compiled = compiler.Compile(document, builtinNodes);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        return new FlowService(store, builtinNodes, compiler, document, compiled.Value);
    }

    /// <summary>全部流程，按文件顺序。</summary>
    public IReadOnlyList<FlowDefinition> All()
    {
        lock (_gate)
        {
            return [.. _compiled.Flows.Select(flow => flow.Model)];
        }
    }

    /// <summary>按名取展开后的流程，不存在时返回错误。</summary>
    public ErrorOr<FlowDefinition> Find(FlowName name)
    {
        ErrorOr<CompiledFlow> flow = FindCompiled(name);

        return flow.IsError ? flow.ErrorsOrEmptyList : flow.Value.Model;
    }

    /// <summary>按名取编译完成的流程，不存在时返回错误。</summary>
    public ErrorOr<CompiledFlow> FindCompiled(FlowName name)
    {
        lock (_gate)
        {
            CompiledFlow? flow = _compiled.Flows.FirstOrDefault(candidate => candidate.Model.Name == name);

            return flow is null ? [FlowErrors.FlowNotFound(name.Value)] : flow;
        }
    }

    /// <summary>从文件导入流程：导入的文件要能独立编译；节点定义按名合并，任一条不合法整体不生效；同名整条覆盖，成功后落盘。</summary>
    public ErrorOr<FlowImport> Import(string source)
    {
        ErrorOr<string> resolved = _store.Resolve(source);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        ErrorOr<FlowDocument> incoming = _store.Read(resolved.Value);
        if (incoming.IsError)
        {
            return incoming.ErrorsOrEmptyList;
        }

        ErrorOr<CompiledDocument> checkedStandalone = _compiler.Compile(incoming.Value, _builtins);
        if (checkedStandalone.IsError)
        {
            List<Error> errors = [.. checkedStandalone.ErrorsOrEmptyList, FlowErrors.ImportStandalone()];

            return errors;
        }

        lock (_gate)
        {
            List<NodeDefinitionDocument> nodes = MergeByName(_document.Nodes, incoming.Value.Nodes, node => node.Name);
            List<FlowDocumentFlow> flows = MergeByName(_document.Flows, incoming.Value.Flows, flow => flow.Name);
            FlowDocument merged = new(nodes, flows);

            ErrorOr<CompiledDocument> compiled = _compiler.Compile(merged, _builtins);
            if (compiled.IsError)
            {
                return compiled.ErrorsOrEmptyList;
            }

            List<string> notes =
            [
                .. incoming.Value.Flows.Select(flow => _document.Flows.Any(existing => existing.Name == flow.Name)
                    ? $"覆盖流程 {flow.Name}"
                    : $"新增流程 {flow.Name}"),
                .. incoming.Value.Nodes.Select(node => $"节点库 {node.Name} 已并入。"),
            ];

            ErrorOr<Success> saved = _store.Save(merged);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _document = merged;
            _compiled = compiled.Value;

            return new FlowImport(resolved.Value, [.. incoming.Value.Flows.Select(flow => new FlowName(flow.Name))], notes);
        }
    }

    /// <summary>按名覆盖合并：同名后写覆盖先写并保留首次出现的位置，其余按原序。</summary>
    private static List<T> MergeByName<T, TKey>(IReadOnlyList<T> current, IReadOnlyList<T> incoming, Func<T, TKey> key)
        where TKey : notnull
    {
        Dictionary<TKey, int> positions = [];
        List<T> merged = [];
        foreach (T item in current.Concat(incoming))
        {
            TKey itemKey = key(item);
            if (positions.TryGetValue(itemKey, out int at))
            {
                merged[at] = item;
            }
            else
            {
                positions[itemKey] = merged.Count;
                merged.Add(item);
            }
        }

        return merged;
    }
}
