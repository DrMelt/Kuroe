using ErrorOr;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Input.Runtime.Tests;

/// <summary>输入通道按任务与步骤保存等待登记，交付调用接收委托并撤销登记。</summary>
public sealed class InputChannelRegistryTests
{
    private static readonly TaskId Task = new(1);
    private static readonly FlowStepPath Step = FlowStepPath.Of("整体", "收话");

    [Fact]
    public void Accept_makes_the_step_pending()
    {
        InputChannelRegistry channel = new();

        channel.Accept(Task, Step, FlowPendingKind.Input, "要做什么", _ => { });

        FlowPendingStep pending = Assert.Single(channel.Pending(Task));
        Assert.Equal(Step, pending.Step);
        Assert.Equal(FlowPendingKind.Input, pending.Kind);
        Assert.Equal("要做什么", pending.Question);
    }

    [Fact]
    public void Accept_replaces_the_registration_for_the_same_step()
    {
        InputChannelRegistry channel = new();
        string? received = null;

        channel.Accept(Task, Step, FlowPendingKind.Input, "旧", _ => received = "旧");
        channel.Accept(Task, Step, FlowPendingKind.Approve, "新", _ => received = "新");

        FlowPendingStep pending = Assert.Single(channel.Pending(Task));
        Assert.Equal(FlowPendingKind.Approve, pending.Kind);
        Assert.Equal("新", pending.Question);

        Assert.False(channel.Deliver(Task, Step, string.Empty).IsError);
        Assert.Equal("新", received);
    }

    [Fact]
    public void Deliver_invokes_the_receiver_and_removes_the_step()
    {
        InputChannelRegistry channel = new();
        string? received = null;
        channel.Accept(Task, Step, FlowPendingKind.Input, null, text => received = text);

        Assert.False(channel.Deliver(Task, Step, "回答").IsError);

        Assert.Equal("回答", received);
        Assert.Empty(channel.Pending(Task));
    }

    [Fact]
    public void Deliver_unknown_step_returns_an_error()
    {
        InputChannelRegistry channel = new();

        Error error = channel.Deliver(Task, Step, "回答").FirstError;

        Assert.Equal(InputErrors.NotAwaiting, error.Code);
    }

    [Fact]
    public void Decline_removes_the_step()
    {
        InputChannelRegistry channel = new();
        channel.Accept(Task, Step, FlowPendingKind.Input, null, _ => { });

        channel.Decline(Task, Step);

        Assert.Empty(channel.Pending(Task));
    }

    [Fact]
    public void Remove_clears_all_steps_of_the_task()
    {
        InputChannelRegistry channel = new();
        channel.Accept(Task, Step, FlowPendingKind.Input, null, _ => { });
        channel.Accept(Task, FlowStepPath.Of("整体", "审阅"), FlowPendingKind.Approve, null, _ => { });

        channel.Remove(Task);

        Assert.Empty(channel.Pending(Task));
    }

    [Fact]
    public void Tasks_are_isolated()
    {
        InputChannelRegistry channel = new();
        TaskId other = new(2);
        channel.Accept(Task, Step, FlowPendingKind.Input, null, _ => { });

        Assert.Empty(channel.Pending(other));
        Assert.True(channel.Deliver(other, Step, "回答").IsError);
    }
}