using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Node.Shared.StaticGraph.Graph;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Node.Input.Tests;

/// <summary>测试用的任务上下文：给出一个由已发布来源驱动的输入侧方案，其余成员不参与。</summary>
internal sealed class StubNodeContext : INodeContext
{
    private static readonly NodePath SourcePath = NodePath.Of("整体", "来源");

    public NodeInputPlan? Plan(NodePath node) => new()
    {
        Groups =
        [
            new InputGroup(
            [
                new InputBinding(
                    NodePath.Of("整体", "输入"),
                    BindingSource.ForNode(SourcePath, new PortName("目标")),
                    new PortName("目标"),
                    PortTypes.Text),
            ]),
        ],
    };

    public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

    public NodeRuntime? Materialized(NodePath node) => null;

    public IReadOnlyList<ContextValue> OutputValues(BindingSource source) => [];

    public ISourceState Source(BindingSource source) => new ReleasedSource();

    /// <summary>测试用的来源状态：已放行，版本固定为 1。</summary>
    private sealed class ReleasedSource : ISourceState
    {
        public bool Released => true;

        public long Revision => 1;
    }
}