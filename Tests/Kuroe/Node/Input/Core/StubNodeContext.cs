using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Node.Input.Tests;

/// <summary>测试用的任务上下文：给出一个由已发布来源驱动的输入侧方案，其余成员不参与。</summary>
internal sealed class StubNodeContext : INodeContext
{
    private static readonly NodePath SourcePath = NodePath.Of("整体", "来源");

    public INodeGraph Graph => throw new NotSupportedException();

    public NodeInputPlan? Plan(NodePath node) => new()
    {
        Groups = [new InputGroup([new InputBinding(SourcePath, new PortName("目标"), PortTypes.Text)])],
    };

    public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

    public NodeRuntime? Materialized(NodePath node) => null;

    public ISourceState Source(NodePath node) => new ReleasedSource();

    /// <summary>测试用的来源状态：已放行，版本固定为 1。</summary>
    private sealed class ReleasedSource : ISourceState
    {
        public bool Released => true;

        public long Revision => 1;
    }
}