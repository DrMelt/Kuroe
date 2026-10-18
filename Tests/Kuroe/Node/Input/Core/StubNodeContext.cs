using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Node.Input.Tests;

/// <summary>测试用的任务上下文：守位用例只把它当登记的分组键，其余成员不参与。</summary>
internal sealed class StubNodeContext : INodeContext
{
    public INodeGraph Graph => throw new NotSupportedException();

    public NodeInputPlan? Plan(NodePath node) => throw new NotSupportedException();

    public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

    public NodeRuntime? Materialized(NodePath node) => throw new NotSupportedException();

    public ISourceState Source(NodePath node) => throw new NotSupportedException();

    public bool HasSplit(NodePath node) => throw new NotSupportedException();

    public IReadOnlyList<int>? SplitItems(NodePath node, BranchName? branch) => throw new NotSupportedException();

    public IReadOnlyList<int>? Items(NodePath node) => throw new NotSupportedException();
}