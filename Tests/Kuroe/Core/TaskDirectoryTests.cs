using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Shared;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>任务工作目录的命名、续号与建立时机。</summary>
public sealed class TaskDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Prepare_names_the_directory_with_task_number_flow_and_title()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;

        string prepared = directory.Prepare(new TaskId(3), new FlowName("审阅"), "修复登录超时").ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal("0003-审阅-修复登录超时", Path.GetFileName(created));
        Assert.Equal(created, prepared);
    }

    [Fact]
    public void Invalid_characters_are_replaced_and_the_flow_segment_keeps_no_hyphen()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;

        directory.Prepare(new TaskId(7), new FlowName("合/同-审阅"), "修复: 登录?").ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal("0007-合_同_审阅-修复_ 登录", Path.GetFileName(created));
    }

    [Fact]
    public void Title_segment_keeps_hyphens()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;

        directory.Prepare(new TaskId(1), new FlowName("默认"), "修复-a-b").ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal("0001-默认-修复-a-b", Path.GetFileName(created));
    }

    [Fact]
    public void Segments_are_truncated_by_character_without_splitting_surrogates()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        string flow = new('流', 25);
        string title = new string('目', 39) + "😀" + new string('后', 5);

        directory.Prepare(new TaskId(1), new FlowName(flow), title).ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal($"0001-{new string('流', 20)}-{new string('目', 39)}😀", Path.GetFileName(created));
    }

    [Fact]
    public void Existing_task_numbers_give_the_next_number()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "0002-审阅-甲"));
        Directory.CreateDirectory(Path.Combine(_root, "0010-审阅-乙"));
        Directory.CreateDirectory(Path.Combine(_root, "0001"));
        Directory.CreateDirectory(Path.Combine(_root, "说明"));

        TaskDirectory directory = TaskDirectory.Create(_root).Value;

        Assert.Equal(11, directory.NextTaskNumber);
    }

    [Fact]
    public void An_occupied_name_reports_a_conflict()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        directory.Prepare(new TaskId(1), new FlowName("默认"), "目标").ThrowIfError();

        ErrorOr<string> second = directory.Prepare(new TaskId(1), new FlowName("默认"), "目标");

        Assert.True(second.IsError);
        Assert.Equal(ErrorCodes.TaskWorkspaceConflict, second.FirstError.Code);
    }

    [Fact]
    public void A_path_over_the_limit_is_rejected()
    {
        string root = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, new string('长', 175));
        TaskDirectory directory = TaskDirectory.Create(root).Value;

        ErrorOr<string> result = directory.Prepare(
            new TaskId(1),
            new FlowName(new string('流', 20)),
            new string('标', 40));

        Assert.True(result.IsError);
        Assert.Equal(ErrorCodes.TaskWorkspaceCreate, result.FirstError.Code);
        Assert.Contains("路径过长", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submitting_a_task_creates_its_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskId id = environment.Submit("修复登录超时");

        string created = Assert.Single(Directory.GetDirectories(Path.Combine(environment.Root, ".kuroe_tasks")));
        Assert.Equal("0001-默认-修复登录超时", Path.GetFileName(created));
        Assert.Equal(1, id.Value);
    }

    [Fact]
    public void An_occupied_task_number_moves_to_the_next_free_number()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string tasks = Path.Combine(environment.Root, ".kuroe_tasks");
        Directory.CreateDirectory(Path.Combine(tasks, "0001-默认-修复登录超时"));

        TaskId id = environment.Submit("修复登录超时");

        Assert.Equal(2, id.Value);
        Assert.Equal(2, Directory.GetDirectories(tasks).Length);
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

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}