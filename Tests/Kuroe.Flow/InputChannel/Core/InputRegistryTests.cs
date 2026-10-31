using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Xunit;

namespace Kuroe.Flow.InputChannel.Tests;

/// <summary>外部输入协议的默认承载：按任务分组记下接收外部输入的节点，交付时按节点路径调节点给的入口。</summary>
public sealed class InputRegistryTests
{
    private static readonly NodePath Input = NodePath.Of("整体", "收话");
    private static readonly NodePath Review = NodePath.Of("整体", "审阅");

    [Fact]
    public void Declared_nodes_are_grouped_by_task()
    {
        InputRegistry channel = new();
        StubContext first = new();
        StubContext second = new();

        channel.Accept(first, Input, _ => { });
        channel.Accept(second, Review, _ => { });

        Assert.Equal(["整体/收话"], Accepting(channel, first));
        Assert.Equal(["整体/审阅"], Accepting(channel, second));
    }

    [Fact]
    public void Declining_a_node_leaves_the_others()
    {
        InputRegistry channel = new();
        StubContext task = new();
        channel.Accept(task, Input, _ => { });
        channel.Accept(task, Review, _ => { });

        channel.Decline(task, Input);
        Assert.Equal(["整体/审阅"], Accepting(channel, task));

        channel.Decline(task, Review);
        Assert.Empty(channel.Accepting(task));
    }

    [Fact]
    public void The_input_reaches_the_node_entry()
    {
        InputRegistry channel = new();
        StubContext task = new();
        List<string> received = [];
        channel.Accept(task, Input, received.Add);

        channel.Deliver(task, Input, "补齐 README");

        Assert.Equal(["补齐 README"], received);
    }

    [Fact]
    public void Delivery_revokes_the_declaration()
    {
        InputRegistry channel = new();
        StubContext task = new();
        channel.Accept(task, Input, _ => { });

        channel.Deliver(task, Input, "补齐 README");

        Assert.Empty(channel.Accepting(task));
        Assert.Throws<InvalidOperationException>(() => channel.Deliver(task, Input, "又"));
    }

    [Fact]
    public void Declining_after_delivery_is_harmless()
    {
        InputRegistry channel = new();
        StubContext task = new();
        channel.Accept(task, Input, _ => { });
        channel.Deliver(task, Input, "补齐 README");

        channel.Decline(task, Input);

        Assert.Empty(channel.Accepting(task));
    }

    [Fact]
    public void Delivery_to_a_node_that_never_declared_is_an_invariant_violation()
    {
        InputRegistry channel = new();
        StubContext task = new();

        Assert.Throws<InvalidOperationException>(() => channel.Deliver(task, Input, "补齐 README"));
        Assert.Empty(channel.Accepting(task));
    }

    [Fact]
    public void Parallel_tasks_keep_their_own_declarations()
    {
        InputRegistry channel = new();
        StubContext[] tasks = [.. Enumerable.Range(0, 8).Select(_ => new StubContext())];
        int delivered = 0;

        Parallel.ForEach(tasks, task =>
        {
            for (int round = 0; round < 200; round++)
            {
                channel.Accept(task, Input, _ => Interlocked.Increment(ref delivered));
                channel.Accept(task, Review, _ => { });
                channel.Decline(task, Review);
                channel.Deliver(task, Input, string.Empty);

                Assert.Empty(channel.Accepting(task));
            }
        });

        Assert.Equal(8 * 200, delivered);
    }

    /// <summary>按路径文本排序的接收节点，通道给出的是无序集合。</summary>
    private static string[] Accepting(InputRegistry channel, StubContext task) =>
        [.. channel.Accepting(task).Select(node => node.Value).OrderBy(value => value, StringComparer.Ordinal)];

    /// <summary>测试用的任务上下文：通道只按它分组，其余成员不参与。</summary>
    private sealed class StubContext : INodeContext
    {
        public INodeGraph Graph => throw new NotSupportedException();

        public NodeInputPlan? Plan(NodePath node) => throw new NotSupportedException();

        public NodeRuntime Runtime(NodePath node) => throw new NotSupportedException();

        public NodeRuntime? Materialized(NodePath node) => throw new NotSupportedException();

        public ISourceState Source(NodePath node) => throw new NotSupportedException();
    }
}