using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>任务域隔离：内置对话任务不进入用户任务域，任务号与执行索引各自按域与全局的边界工作。</summary>
public sealed class TaskDomainTests
{
    [Fact]
    public async Task A_dialogue_round_does_not_create_a_user_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);

        Assert.False(reply.IsError);
        Assert.Empty(environment.TaskReader.Snapshots());
        Assert.Empty(environment.TaskReader.LiveExecutions());
        Assert.Single(environment.Runner.StartedCode);
    }

    [Fact]
    public async Task The_first_user_task_keeps_number_one_after_dialogue()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);
        TaskId id = environment.Submit("修复登录超时");

        Assert.False(reply.IsError);
        Assert.Equal(new TaskId(1), id);
        string tasks = Path.Combine(environment.Root, ".kuroe_tasks");
        string created = Assert.Single(Directory.GetDirectories(tasks), path => Path.GetFileName(path) != ".records");
        Assert.Equal(id.Value, Path.GetFileName(created));
    }

    [Fact]
    public async Task The_user_task_domain_cannot_find_the_dialogue_task_or_its_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);
        IExecution round = Assert.Single(environment.Runner.StartedCode);

        Assert.False(reply.IsError);
        Assert.True(environment.TaskReader.Find(new TaskId(round.Request.Owner.Value)).IsError);
        Assert.True(environment.TaskReader.FindExecution(round.Id).IsError);
        Assert.False(environment.Executions.FindOwned(round.Id).IsError);
    }

    [Fact]
    public async Task Clearing_settled_user_tasks_does_not_touch_the_dialogue_task()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("第一问", null);
        IReadOnlyList<TaskId> cleared = environment.Registry.ClearSettled();
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("第二问", null);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Empty(cleared);
        Assert.Equal(2, environment.Runner.StartedCode.Count);
    }

    [Fact]
    public async Task Dialogue_executions_share_the_global_execution_number_sequence()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);
        IExecution dialogue = Assert.Single(environment.Runner.StartedCode);
        TaskId id = environment.Submit("目标");
        TaskSnapshot done = await environment.SettleAsync(id);

        Assert.False(reply.IsError);
        Assert.Equal(1, dialogue.Id.Value);
        IReadOnlyList<Kuroe.Execution.Shared.ExecutionSnapshot> executions = [.. done.Executions];
        Assert.NotEmpty(executions);
        Assert.All(executions, execution => Assert.True(execution.Id.Value > dialogue.Id.Value));
        Assert.All(executions, execution =>
            Assert.Equal(id, environment.Executions.FindOwned(
                new Kuroe.Execution.Shared.ExecutionId(execution.Id.Value)).ThrowIfError().Owner.Id));
    }

    [Fact]
    public async Task A_user_task_and_the_dialogue_task_advance_independently()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.CodeReply = _ => "回复";

        ErrorOr<Success> first = await environment.Dialogue.ReplyAsync("你好", null);
        TaskId id = environment.Submit("目标");
        TaskSnapshot done = await environment.SettleAsync(id);
        ErrorOr<Success> second = await environment.Dialogue.ReplyAsync("再问", null);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Equal(new TaskId(1), id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, environment.Runner.StartedCode.Count(execution => execution.Request.Target.Value == "回话"));
    }
}
