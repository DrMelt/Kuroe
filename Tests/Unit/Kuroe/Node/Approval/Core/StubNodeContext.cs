using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Readiness;

using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Node.Approval.Tests;

/// <summary>测试用的任务上下文：守位用例只把它当登记的分组键，其余成员不参与。</summary>
internal sealed class StubNodeContext : INodeContext
{
    public TaskId Id { get; } = new(1);

    public NodeInputPlan? Plan(NodePath node) => throw new NotSupportedException();

    public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

    public NodeRuntime? Materialized(NodePath node) => throw new NotSupportedException();

    public IReadOnlyList<ContextValue> OutputValues(BindingSource source) => throw new NotSupportedException();

    public ISourceState Source(BindingSource source) => throw new NotSupportedException();
}