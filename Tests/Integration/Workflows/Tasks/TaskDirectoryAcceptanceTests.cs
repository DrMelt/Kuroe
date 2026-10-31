using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Integration.Tests.Workflows.Tasks;

/// <summary>任务目录接入任务提交：提交建目录，旧目录不影响新提交，对话任务不建目录。</summary>
public sealed class TaskDirectoryAcceptanceTests
{
    [Fact]
    public void Submitting_a_task_creates_its_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("修复登录超时");

        string tasks = Path.Combine(environment.Root, ".kuroe_tasks");
        string created = Assert.Single(Directory.GetDirectories(tasks), path => Path.GetFileName(path) != ".checkpoints");
        Assert.Equal($"{id.Value}-默认-修复登录超时", Path.GetFileName(created));
        Assert.Equal(new TaskId(1), id);
    }

    [Fact]
    public void Old_task_directories_do_not_affect_a_new_submission()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string tasks = Path.Combine(environment.Root, ".kuroe_tasks");
        Directory.CreateDirectory(Path.Combine(tasks, "0001-默认-修复登录超时"));

        TaskId id = environment.Submit("修复登录超时");

        Assert.Equal(2, Directory.GetDirectories(tasks).Count(path => Path.GetFileName(path) != ".checkpoints"));
        Assert.NotNull(Directory.GetDirectories(tasks, $"{id.Value}-*").FirstOrDefault());
    }

    [Fact]
    public async Task The_dialogue_task_has_no_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.Reply = _ => "回复你好";

        ErrorOr<Success> reply = await environment.Dialogue.ReplyAsync("你好", null);

        Assert.False(reply.IsError);
        Assert.False(Directory.Exists(Path.Combine(environment.Root, ".kuroe_tasks")));
    }
}
