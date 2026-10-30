using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Workflows.Flows.Files;

namespace Kuroe.Workflows.Flows;

/// <summary>运行期的流程模板集合。描述文档是授权来源，编译结果是运行期来源，落盘交给 FlowStore。</summary>
public sealed class FlowService : IFlowCatalog, IFlowRuntimeCatalog, IFlowSnapshotSource, IFlowSnapshotCompiler
{
    private readonly FlowStore _store;
    private readonly IReadOnlyList<NodeSpec> _builtins;
    private readonly IFlowDescriptionCompiler _compiler;
    private readonly bool _tolerateFailures;
    private readonly Lock _gate = new();
    private FlowDocument _document;
    private IReadOnlyList<CompiledFlow> _compiled;
    private IReadOnlyList<FlowFailure> _failures;

    private FlowService(
        FlowStore store,
        IReadOnlyList<NodeSpec> builtins,
        IFlowDescriptionCompiler compiler,
        bool tolerateFailures,
        FlowDocument document,
        IReadOnlyList<CompiledFlow> compiled,
        IReadOnlyList<FlowFailure> failures)
    {
        _store = store;
        _builtins = builtins;
        _compiler = compiler;
        _tolerateFailures = tolerateFailures;
        _document = document;
        _compiled = compiled;
        _failures = failures;
    }

    /// <summary>从流程文件编译模板集合。严格模式任一条流程不合法时返回全部错误，宽容模式保留失败流程。</summary>
    internal static ErrorOr<FlowService> Create(
        FlowStore store,
        IReadOnlyList<NodeSpec> builtinNodes,
        IFlowDescriptionCompiler compiler,
        bool tolerateFailures)
    {
        ErrorOr<FlowDocument?> loaded = store.Load();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        FlowDocument document = loaded.Value ?? new FlowDocument([], []);
        ErrorOr<CompileResult> compiled = Compile(document, builtinNodes, compiler);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        if (!tolerateFailures && compiled.Value.Failures.Count > 0)
        {
            return [.. compiled.Value.Failures.SelectMany(failure => failure.Errors)];
        }

        return new FlowService(
            store,
            builtinNodes,
            compiler,
            tolerateFailures,
            document,
            compiled.Value.Flows,
            compiled.Value.Failures);
    }

    /// <summary>不可用流程：流程名与编译错误，按文件顺序。</summary>
    public IReadOnlyList<FlowFailure> Failures
    {
        get
        {
            lock (_gate)
            {
                return [.. _failures];
            }
        }
    }

    /// <summary>全部流程，按文件顺序。</summary>
    public IReadOnlyList<FlowDefinition> All()
    {
        lock (_gate)
        {
            return [.. _compiled.Select(flow => flow.Model)];
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
            CompiledFlow? flow = _compiled.FirstOrDefault(candidate => candidate.Model.Name == name);
            if (flow is not null)
            {
                return flow;
            }

            FlowFailure? failure = _failures.FirstOrDefault(candidate => candidate.Name == name);

            return failure is null ? [FlowErrors.FlowNotFound(name.Value)] : [.. failure.Errors];
        }
    }

    ErrorOr<FlowSnapshot> IFlowSnapshotSource.Capture(FlowName name)
    {
        lock (_gate)
        {
            FlowDocumentFlow? flow = _document.Flows.FirstOrDefault(candidate => candidate.Name == name.Value);
            if (flow is null)
            {
                return [FlowErrors.FlowNotFound(name.Value)];
            }

            ErrorOr<string> text = _store.Render(new FlowDocument(_document.Nodes, [flow]));

            return text.IsError ? text.ErrorsOrEmptyList : new FlowSnapshot(name, text.Value);
        }
    }

    ErrorOr<CompiledFlow> IFlowSnapshotCompiler.Compile(FlowSnapshot snapshot)
    {
        ErrorOr<FlowDocument> document = _store.ReadText(snapshot.Document);
        if (document.IsError)
        {
            return [Error.Failure(
                FlowErrorCodes.CheckpointFlowSnapshot,
                $"流程快照不合法：{document.FirstError.Description}")];
        }

        ErrorOr<CompileResult> compiled = Compile(document.Value, _builtins, _compiler);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        CompiledFlow? flow = compiled.Value.Flows.FirstOrDefault(candidate => candidate.Model.Name == snapshot.Name);

        return flow is null
            ? [Error.Failure(FlowErrorCodes.CheckpointFlowSnapshot, $"流程快照中没有流程 {snapshot.Name}。")]
            : flow;
    }

    /// <summary>从文件导入流程：严格模式任一流程不合法即整体不生效；宽容模式只保存可编译流程，丢弃不可编译流程并逐条说明。</summary>
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

        return _tolerateFailures
            ? ImportTolerantly(resolved.Value, incoming.Value)
            : ImportStrictly(resolved.Value, incoming.Value);
    }

    /// <summary>严格导入：导入文件与合并结果都必须整体编译通过。</summary>
    private ErrorOr<FlowImport> ImportStrictly(string source, FlowDocument incoming)
    {
        ErrorOr<CompiledDocument> checkedStandalone = _compiler.Compile(incoming, _builtins);
        if (checkedStandalone.IsError)
        {
            return [.. checkedStandalone.ErrorsOrEmptyList, FlowErrors.ImportStandalone()];
        }

        lock (_gate)
        {
            List<NodeDefinitionDocument> nodes = MergeByName(_document.Nodes, incoming.Nodes, node => node.Name);
            List<FlowDocumentFlow> flows = MergeByName(_document.Flows, incoming.Flows, flow => flow.Name);
            FlowDocument merged = new(nodes, flows);

            ErrorOr<CompiledDocument> compiled = _compiler.Compile(merged, _builtins);
            if (compiled.IsError)
            {
                return compiled.ErrorsOrEmptyList;
            }

            List<string> notes =
            [
                .. incoming.Flows.Select(flow => _document.Flows.Any(existing => existing.Name == flow.Name)
                    ? $"覆盖流程 {flow.Name}"
                    : $"新增流程 {flow.Name}"),
                .. incoming.Nodes.Select(node => $"节点库 {node.Name} 已并入。"),
            ];

            ErrorOr<Success> saved = _store.Save(merged);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _document = merged;
            _compiled = compiled.Value.Flows;
            _failures = [];

            return new FlowImport(source, [.. incoming.Flows.Select(flow => new FlowName(flow.Name))], notes);
        }
    }

    /// <summary>宽容导入：节点库必须整体合法；只合并可编译流程，不可编译流程丢弃并给出原因。</summary>
    private ErrorOr<FlowImport> ImportTolerantly(string source, FlowDocument incoming)
    {
        ErrorOr<CompiledDocument> library = _compiler.Compile(new FlowDocument(incoming.Nodes, []), _builtins);
        if (library.IsError)
        {
            return library.ErrorsOrEmptyList;
        }

        ErrorOr<CompileResult> checkedStandalone = Compile(incoming, _builtins, _compiler);
        if (checkedStandalone.IsError)
        {
            return [.. checkedStandalone.ErrorsOrEmptyList, FlowErrors.ImportStandalone()];
        }

        HashSet<FlowName> accepted = [.. checkedStandalone.Value.Flows.Select(flow => flow.Model.Name)];
        List<FlowDocumentFlow> acceptedFlows =
            [.. incoming.Flows.Where(flow => accepted.Contains(new FlowName(flow.Name.Trim())))];

        lock (_gate)
        {
            List<NodeDefinitionDocument> nodes = MergeByName(_document.Nodes, incoming.Nodes, node => node.Name);
            List<FlowDocumentFlow> flows = MergeByName(_document.Flows, acceptedFlows, flow => flow.Name);
            FlowDocument merged = new(nodes, flows);

            ErrorOr<CompileResult> compiled = Compile(merged, _builtins, _compiler);
            if (compiled.IsError)
            {
                return compiled.ErrorsOrEmptyList;
            }

            HashSet<FlowName> knownFailures = [.. _failures.Select(failure => failure.Name)];
            List<FlowFailure> rejected =
            [
                .. compiled.Value.Failures.Where(failure =>
                    accepted.Contains(failure.Name) || !knownFailures.Contains(failure.Name)),
            ];
            if (rejected.Count > 0)
            {
                return [.. rejected.SelectMany(failure => failure.Errors)];
            }

            List<string> notes =
            [
                .. acceptedFlows.Select(flow => _document.Flows.Any(existing => existing.Name.Trim() == flow.Name.Trim())
                    ? $"覆盖流程 {flow.Name.Trim()}"
                    : $"新增流程 {flow.Name.Trim()}"),
                .. incoming.Nodes.Select(node => $"节点库 {node.Name} 已并入。"),
                .. checkedStandalone.Value.Failures.Select(failure =>
                    _document.Flows.Any(existing => existing.Name.Trim() == failure.Name.Value)
                        ? $"流程 {failure.Name.Value} 未导入：{Reason(failure)}；保留原流程。"
                        : $"流程 {failure.Name.Value} 未导入：{Reason(failure)}"),
            ];

            ErrorOr<Success> saved = Result.Success;
            if (acceptedFlows.Count > 0 || incoming.Nodes.Count > 0)
            {
                saved = _store.Save(merged);
            }

            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _document = merged;
            _compiled = compiled.Value.Flows;
            _failures = compiled.Value.Failures;

            return new FlowImport(
                source,
                [.. acceptedFlows.Select(flow => new FlowName(flow.Name.Trim()))],
                notes);
        }
    }

    /// <summary>一次流程库编译的结果：可用流程与不可用流程。</summary>
    private sealed record CompileResult(
        IReadOnlyList<CompiledFlow> Flows,
        IReadOnlyList<FlowFailure> Failures);

    /// <summary>按流程隔离编译：整体成功直接用结果，整体失败时逐条流程编译。</summary>
    private static ErrorOr<CompileResult> Compile(
        FlowDocument document,
        IReadOnlyList<NodeSpec> builtins,
        IFlowDescriptionCompiler compiler)
    {
        List<Error> duplicates = [.. Duplicated(document)];
        if (duplicates.Count > 0)
        {
            return duplicates;
        }

        ErrorOr<CompiledDocument> whole = compiler.Compile(document, builtins);
        if (!whole.IsError)
        {
            return new CompileResult(whole.Value.Flows, []);
        }

        if (document.Flows.Count == 0)
        {
            return whole.ErrorsOrEmptyList;
        }

        List<CompiledFlow> flows = [];
        List<FlowFailure> failures = [];
        foreach (FlowDocumentFlow flow in document.Flows)
        {
            ErrorOr<CompiledDocument> single = compiler.Compile(
                new FlowDocument(document.Nodes, [flow]),
                builtins);
            if (single.IsError)
            {
                ErrorOr<FlowName> parsed = FlowName.Create(flow.Name);
                FlowName name = parsed.IsError ? new FlowName(flow.Name) : parsed.Value;

                failures.Add(new FlowFailure(name, single.ErrorsOrEmptyList));
            }
            else
            {
                flows.Add(single.Value.Flows[0]);
            }
        }

        return new CompileResult(flows, failures);
    }

    /// <summary>流程名重复：同一文档内名字是流程的唯一标识。</summary>
    private static IEnumerable<Error> Duplicated(FlowDocument document) =>
        document.Flows
            .Select(flow => (Flow: flow, Name: FlowName.Create(flow.Name)))
            .Where(entry => !entry.Name.IsError)
            .GroupBy(entry => entry.Name.Value)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1)
                .Select(entry => FlowErrors.Body(entry.Flow.Name, "流程名重复。")));

    /// <summary>失败原因取第一条错误说明。</summary>
    private static string Reason(FlowFailure failure) =>
        failure.Errors.Count > 0 ? failure.Errors[0].Description : "流程不可用。";

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
