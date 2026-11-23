using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.Flow.Library.Files;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Json.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Library;

/// <summary>运行期的流程模板库。流程描述是授权来源，编译结果是运行期来源，落盘交给 <see cref="FlowStore"/>。查询与快照由组合组件提供。</summary>
public sealed class FlowLibrary : IFlowLibrary
{
    private readonly FlowStore _store;
    private readonly IFlowCompiler _compiler;
    private readonly bool _tolerateFailures;
    private readonly Lock _gate = new();
    private FlowLibraryState _state;

    private FlowLibrary(FlowStore store, IFlowCompiler compiler, bool tolerateFailures, FlowLibraryState state)
    {
        _store = store;
        _compiler = compiler;
        _tolerateFailures = tolerateFailures;
        _state = state;
    }

    /// <summary>当前状态，读取方一次取得一致视图。</summary>
    internal FlowLibraryState Current
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>从流程文件编译模板集合。严格模式任一条流程不合法时返回全部错误，宽容模式保留失败流程。</summary>
    public static ErrorOr<FlowLibrary> Create(
        FlowStore store,
        IFlowCompiler compiler,
        bool tolerateFailures)
    {
        ErrorOr<FlowDescriptionSet?> loaded = store.Load();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        FlowDescriptionSet document = loaded.Value ?? new FlowDescriptionSet([], []);
        ErrorOr<FlowSetCompilation.CompileResult> compiled = FlowSetCompilation.Compile(document, compiler);
        if (compiled.IsError)
        {
            return compiled.ErrorsOrEmptyList;
        }

        if (!tolerateFailures && compiled.Value.Failures.Count > 0)
        {
            return [.. compiled.Value.Failures.SelectMany(failure => failure.Errors)];
        }

        return new FlowLibrary(
            store,
            compiler,
            tolerateFailures,
            new FlowLibraryState(document, compiled.Value.Flows, compiled.Value.Failures));
    }

    /// <summary>不可用流程：流程名与编译错误，按文件顺序。</summary>
    public IReadOnlyList<FlowFailure> Failures => Current.Failures;

    /// <summary>从文件导入：按顶层字段判定导入节点库还是流程。严格模式任一流程不合法即整体不生效；宽容模式只保存可编译流程。</summary>
    public ErrorOr<FlowImport> Import(string source)
    {
        ErrorOr<string> resolved = _store.Resolve(source);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        ErrorOr<FlowImportContent> incoming = _store.ReadFile(resolved.Value);
        if (incoming.IsError)
        {
            return incoming.ErrorsOrEmptyList;
        }

        return incoming.Value.Kind switch
        {
            FlowImportKind.Nodes => ImportNodes(resolved.Value, incoming.Value.Nodes),
            FlowImportKind.Flows => _tolerateFailures
                ? ImportFlowsTolerantly(resolved.Value, incoming.Value.Flows)
                : ImportFlowsStrictly(resolved.Value, incoming.Value.Flows),
            _ => throw new InvalidOperationException($"不认识的导入文件类别 {incoming.Value.Kind}。"),
        };
    }

    /// <summary>导入节点库：节点库本身必须合法，且合并后不能破坏当前可用的流程。</summary>
    private ErrorOr<FlowImport> ImportNodes(string source, IReadOnlyList<NodeDefinitionDescription> incoming)
    {
        lock (_gate)
        {
            FlowLibraryState state = _state;
            List<Error> duplicates =
            [
                .. incoming
                    .GroupBy(node => node.Name)
                    .Where(group => group.Count() > 1)
                    .SelectMany(group => group.Skip(1).Select(node =>
                        Error.Validation(FlowErrorCodes.FlowEntry, $"节点库定义 {node.Name}：节点名重复。"))),
            ];
            if (duplicates.Count > 0)
            {
                return duplicates;
            }

            List<NodeDefinitionDescription> nodes = MergeByName(state.Document.Nodes, incoming, node => node.Name);
            ErrorOr<IReadOnlyList<FlowGraph>> library = _compiler.Compile(new FlowDescriptionSet(nodes, []));
            if (library.IsError)
            {
                return library.ErrorsOrEmptyList;
            }

            ErrorOr<FlowSetCompilation.CompileResult> compiled =
                FlowSetCompilation.Compile(new FlowDescriptionSet(nodes, state.Document.Flows), _compiler);
            if (compiled.IsError)
            {
                return compiled.ErrorsOrEmptyList;
            }

            if (compiled.Value.Failures.Count > 0)
            {
                if (!_tolerateFailures)
                {
                    return [.. compiled.Value.Failures.SelectMany(failure => failure.Errors)];
                }

                HashSet<FlowName> available = [.. state.Compiled.Select(flow => flow.Definition.Name)];
                List<FlowFailure> broken =
                    [.. compiled.Value.Failures.Where(failure => available.Contains(failure.Name))];
                if (broken.Count > 0)
                {
                    return [.. broken.SelectMany(failure => failure.Errors)];
                }
            }

            List<string> notes =
            [
                .. incoming.Select(node => state.Document.Nodes.Any(existing => existing.Name == node.Name)
                    ? $"覆盖节点 {node.Name}"
                    : $"新增节点 {node.Name}"),
            ];

            ErrorOr<Success> saved = _store.SaveNodes(nodes);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _state = new FlowLibraryState(
                new FlowDescriptionSet(nodes, state.Document.Flows),
                compiled.Value.Flows,
                compiled.Value.Failures);

            return new FlowImport(source, [], [.. incoming.Select(node => node.Name)], notes);
        }
    }

    /// <summary>严格导入流程：合并节点库后的全部流程必须整体编译通过。</summary>
    private ErrorOr<FlowImport> ImportFlowsStrictly(string source, IReadOnlyList<FlowEntry> incoming)
    {
        List<Error> duplicates = [.. FlowSetCompilation.Duplicated(incoming)];
        if (duplicates.Count > 0)
        {
            return duplicates;
        }

        lock (_gate)
        {
            FlowLibraryState state = _state;
            List<FlowEntry> flows = MergeByName(state.Document.Flows, incoming, flow => flow.Name);
            ErrorOr<IReadOnlyList<FlowGraph>> compiled =
                _compiler.Compile(new FlowDescriptionSet(state.Document.Nodes, flows));
            if (compiled.IsError)
            {
                return compiled.ErrorsOrEmptyList;
            }

            List<string> notes =
            [
                .. incoming.Select(flow => state.Document.Flows.Any(existing => existing.Name == flow.Name)
                    ? $"覆盖流程 {flow.Name}"
                    : $"新增流程 {flow.Name}"),
            ];

            ErrorOr<Success> saved = _store.SaveFlows(flows);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _state = new FlowLibraryState(
                new FlowDescriptionSet(state.Document.Nodes, flows),
                compiled.Value,
                []);

            return new FlowImport(
                source,
                [.. incoming.Select(flow => new FlowName(flow.Name))],
                [],
                notes);
        }
    }

    /// <summary>宽容导入流程：按当前节点库只合并可编译流程，不可编译流程丢弃并给出原因。</summary>
    private ErrorOr<FlowImport> ImportFlowsTolerantly(string source, IReadOnlyList<FlowEntry> incoming)
    {
        lock (_gate)
        {
            FlowLibraryState state = _state;
            ErrorOr<FlowSetCompilation.CompileResult> checkedIncoming =
                FlowSetCompilation.Compile(new FlowDescriptionSet(state.Document.Nodes, incoming), _compiler);
            if (checkedIncoming.IsError)
            {
                return checkedIncoming.ErrorsOrEmptyList;
            }

            HashSet<FlowName> accepted = [.. checkedIncoming.Value.Flows.Select(flow => flow.Definition.Name)];
            List<FlowEntry> acceptedFlows =
                [.. incoming.Where(flow => accepted.Contains(new FlowName(flow.Name.Trim())))];

            List<FlowEntry> flows = MergeByName(state.Document.Flows, acceptedFlows, flow => flow.Name);
            ErrorOr<FlowSetCompilation.CompileResult> compiled =
                FlowSetCompilation.Compile(new FlowDescriptionSet(state.Document.Nodes, flows), _compiler);
            if (compiled.IsError)
            {
                return compiled.ErrorsOrEmptyList;
            }

            HashSet<FlowName> knownFailures = [.. state.Failures.Select(failure => failure.Name)];
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
                .. acceptedFlows.Select(flow => state.Document.Flows.Any(existing => existing.Name.Trim() == flow.Name.Trim())
                    ? $"覆盖流程 {flow.Name.Trim()}"
                    : $"新增流程 {flow.Name.Trim()}"),
                .. checkedIncoming.Value.Failures.Select(failure =>
                    state.Document.Flows.Any(existing => existing.Name.Trim() == failure.Name.Value)
                        ? $"流程 {failure.Name.Value} 未导入：{Reason(failure)}；保留原流程。"
                        : $"流程 {failure.Name.Value} 未导入：{Reason(failure)}"),
            ];

            ErrorOr<Success> saved = acceptedFlows.Count > 0 ? _store.SaveFlows(flows) : Result.Success;
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _state = new FlowLibraryState(
                new FlowDescriptionSet(state.Document.Nodes, flows),
                compiled.Value.Flows,
                compiled.Value.Failures);

            return new FlowImport(
                source,
                [.. acceptedFlows.Select(flow => new FlowName(flow.Name.Trim()))],
                [],
                notes);
        }
    }

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
