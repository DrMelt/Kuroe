using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Library;

/// <summary>流程描述集合的编译：整体成功直接用结果，整体失败时逐条流程编译，把失败隔离在单条流程。</summary>
internal static class FlowSetCompilation
{
    /// <summary>一次流程库编译的结果：可用流程与不可用流程。</summary>
    internal sealed record CompileResult(
        IReadOnlyList<FlowGraph> Flows,
        IReadOnlyList<FlowFailure> Failures);

    /// <summary>按流程隔离编译一份描述集合。</summary>
    internal static ErrorOr<CompileResult> Compile(FlowDescriptionSet document, IFlowCompiler compiler)
    {
        List<Error> duplicates = [.. Duplicated(document.Flows)];
        if (duplicates.Count > 0)
        {
            return duplicates;
        }

        ErrorOr<IReadOnlyList<FlowGraph>> whole = compiler.Compile(document);
        if (!whole.IsError)
        {
            return new CompileResult(whole.Value, []);
        }

        if (document.Flows.Count == 0)
        {
            return whole.ErrorsOrEmptyList;
        }

        List<FlowGraph> flows = [];
        List<FlowFailure> failures = [];
        foreach (FlowEntry flow in document.Flows)
        {
            ErrorOr<IReadOnlyList<FlowGraph>> single = compiler.Compile(
                new FlowDescriptionSet(document.Nodes, [flow]));
            if (single.IsError)
            {
                ErrorOr<FlowName> parsed = FlowName.Create(flow.Name);
                FlowName name = parsed.IsError ? new FlowName(flow.Name) : parsed.Value;

                failures.Add(new FlowFailure(name, single.ErrorsOrEmptyList));
            }
            else
            {
                flows.Add(single.Value[0]);
            }
        }

        return new CompileResult(flows, failures);
    }

    /// <summary>流程名重复：同一文档内名字是流程的唯一标识。</summary>
    internal static IEnumerable<Error> Duplicated(IReadOnlyList<FlowEntry> flows) =>
        flows
            .Select(flow => (Flow: flow, Name: FlowName.Create(flow.Name)))
            .Where(entry => !entry.Name.IsError)
            .GroupBy(entry => entry.Name.Value)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1)
                .Select(entry => FlowErrors.Body(entry.Flow.Name, "流程名重复。")));
}
