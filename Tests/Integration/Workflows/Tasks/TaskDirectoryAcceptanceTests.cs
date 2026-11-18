using ErrorOr;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Workflows.Tasks;

/// <summary>任务目录接入任务提交：提交建目录，其他目录不影响新提交，对话任务不建目录。</summary>
public sealed class TaskDirectoryAcceptanceTests
{
    [Fact(DisplayName = "提交任务创建其目录")]
    public void Submitting_a_task_creates_its_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("修复登录超时");

        string tasks = Path.Combine(environment.Root, ".kuroe_tasks");
        string created = Assert.Single(Directory.GetDirectories(tasks), path => Path.GetFileName(path) != ".records");
        Assert.Equal(id.Value, Path.GetFileName(created));
        Assert.Equal(new TaskId(1), id);
    }

    [Fact(DisplayName = "无关目录不影响新提交")]
    public void Unrelated_directories_do_not_affect_a_new_submission()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string tasks = Path.Combine(environment.Root, ".kuroe_tasks");
        Directory.CreateDirectory(Path.Combine(tasks, "0001-默认-修复登录超时"));

        TaskId id = environment.Submit("修复登录超时");

        Assert.Equal(2, Directory.GetDirectories(tasks).Count(path => Path.GetFileName(path) != ".records"));
        Assert.True(Directory.Exists(Path.Combine(tasks, id.Value)), $"{Path.Combine(tasks, id.Value)} 不存在。");
    }

    [Fact(DisplayName = "对话任务没有任务目录")]
    public async Task The_dialogue_task_has_no_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.SayAsync("你好", null);

        Assert.False(reply.IsError, reply.ErrorsText());
        Assert.False(Directory.Exists(Path.Combine(environment.Root, ".kuroe_tasks")), $"{Path.Combine(environment.Root, ".kuroe_tasks")} 不应存在。");
    }
}
