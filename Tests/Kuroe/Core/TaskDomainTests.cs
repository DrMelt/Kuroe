using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Tests.Support;
using Xunit;

namespace Kuroe.Tests;

/// <summary>任务域隔离：内置对话任务不进入用户任务域，任务号与执行索引各自按域与全局的边界工作。</summary>
public sealed class TaskDomainTests
{
    [Fact]
    public async Task A_dialogue_round_does_not_create_a_user_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);

        Assert.False(reply.IsError);
        Assert.Empty(environment.Registry.Snapshots());
        Assert.Empty(environment.Registry.LiveExecutions());
        Assert.Single(environment.Runner.Started);
    }

    [Fact]
    public async Task The_first_user_task_keeps_number_one_after_dialogue()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);
        TaskId id = environment.Submit("修复登录超时");

        Assert.False(reply.IsError);
        Assert.Equal(1, id.Value);
        string created = Assert.Single(Directory.GetDirectories(Path.Combine(environment.Root, ".kuroe_tasks")));
        Assert.Equal("0001-默认-修复登录超时", Path.GetFileName(created));
    }

    [Fact]
    public async Task The_user_task_domain_cannot_find_the_dialogue_task_or_its_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);
        IExecution round = Assert.Single(environment.Runner.Started);

        Assert.False(reply.IsError);
        Assert.True(environment.Registry.Find(round.Input.Task).IsError);
        Assert.True(environment.Registry.FindExecution(round.Id).IsError);
        Assert.False(environment.Executions.FindOwned(round.Id).IsError);
    }

    [Fact]
    public async Task Clearing_settled_user_tasks_does_not_touch_the_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", null);
        int cleared = environment.Registry.ClearSettled();
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Equal(0, cleared);
        Assert.Equal(2, environment.Runner.Started.Count);
    }

    [Fact]
    public async Task Dialogue_executions_share_the_global_execution_number_sequence()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);
        IExecution dialogue = Assert.Single(environment.Runner.Started);
        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.False(reply.IsError);
        Assert.Equal(1, dialogue.Id.Value);
        IReadOnlyList<ExecutionSnapshot> executions = [.. done.Executables.SelectMany(node => node.Executions)];
        Assert.NotEmpty(executions);
        Assert.All(executions, execution => Assert.True(execution.Id.Value > dialogue.Id.Value));
        Assert.All(executions, execution =>
            Assert.Equal(id, environment.Executions.FindOwned(execution.Id).ThrowIfError().Owner.Id));
    }

    [Fact]
    public async Task A_user_task_and_the_dialogue_task_advance_independently()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = execution => $"回复{execution.Input.ExecutionCount}";

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("你好", null);
        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("再问", null);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Equal(1, id.Value);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, environment.Runner.Started.Count(execution => execution.Input.Node.Label.Value == "回话"));
    }
}
