using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Node.Approval.Tests;

/// <summary>审批节点的运行期对象：取消后不再声明接收外部输入。</summary>
public sealed class ApprovalRuntimeTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "审阅");

    [Fact]
    public void Canceled_node_never_declares_again()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        ApprovalRuntime runtime = Assert.IsType<ApprovalRuntime>(
            new ApprovalNodeType(null, channel).Create(Node(channel), context));

        runtime.Cancel();
        runtime.Activate(context);

        Assert.Empty(channel.Declared);
        Assert.Equal([Path], channel.Declined);
    }

    private static ExecutableNode Node(IInputChannel channel) => new()
    {
        Path = Path,
        Type = new ApprovalNodeType(null, channel),
        Execution = new ExecutableSpec(),
        Model = null,
        Outputs = [],
        MaxRuns = ExecutableNode.DefaultMaxRuns,
    };
}