using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;

namespace Kuroe.Node.Approval.Tests;

/// <summary>测试用的任务上下文：守位用例只把它当登记的分组键，其余成员不参与。</summary>
internal sealed class StubNodeContext : INodeContext
{
    public NodeInputPlan? Plan(NodePath node) => throw new NotSupportedException();

    public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

    public NodeRuntime? Materialized(NodePath node) => throw new NotSupportedException();

    public IReadOnlyList<ContextValue> OutputValues(BindingSource source) => throw new NotSupportedException();

    public ISourceState Source(BindingSource source) => throw new NotSupportedException();
}