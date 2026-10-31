using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Shared;
using Kuroe.Tests.Support;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests.Workflows.Tasks;

/// <summary>任务工作目录的命名与建立。</summary>
public sealed class TaskDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Prepare_names_the_directory_with_task_key_flow_and_title()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        TaskId id = new(3);

        string prepared = directory.Prepare(id, new FlowName("审阅"), "修复登录超时").ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal($"{id.Value}-审阅-修复登录超时", Path.GetFileName(created));
        Assert.Equal(created, prepared);
        Assert.Single(Directory.GetDirectories(_root, $"{id.Value}-*"));
    }

    [Fact]
    public void Invalid_characters_are_replaced_and_the_flow_segment_keeps_no_hyphen()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        TaskId id = new(7);

        directory.Prepare(id, new FlowName("合/同-审阅"), "修复: 登录?").ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal($"{id.Value}-合_同_审阅-修复_ 登录", Path.GetFileName(created));
    }

    [Fact]
    public void Title_segment_keeps_hyphens()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        TaskId id = new(1);

        directory.Prepare(id, new FlowName("默认"), "修复-a-b").ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal($"{id.Value}-默认-修复-a-b", Path.GetFileName(created));
    }

    [Fact]
    public void Segments_are_truncated_by_character_without_splitting_surrogates()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        TaskId id = new(1);
        string flow = new('流', 25);
        string title = new string('目', 39) + "😀" + new string('后', 5);

        directory.Prepare(id, new FlowName(flow), title).ThrowIfError();

        string created = Assert.Single(Directory.GetDirectories(_root));
        Assert.Equal($"{id.Value}-{new string('流', 20)}-{new string('目', 39)}😀", Path.GetFileName(created));
    }

    [Fact]
    public void Old_numbered_directories_are_ignored()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "0002-审阅-甲"));
        Directory.CreateDirectory(Path.Combine(_root, "0010-审阅-乙"));

        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        TaskId id = new(5);

        string prepared = directory.Prepare(id, new FlowName("默认"), "目标").ThrowIfError();

        Assert.Equal(Path.Combine(_root, $"{id.Value}-默认-目标"), prepared);
        Assert.Equal(3, Directory.GetDirectories(_root).Length);
        Assert.Null(directory.Find(new TaskId(999)));
        Assert.Equal(prepared, directory.Find(id));
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
    public void A_root_occupied_by_a_file_reports_the_workspace_error()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        File.WriteAllText(root, "occupied");

        try
        {
            TaskDirectory directory = TaskDirectory.Create(root).Value;

            ErrorOr<string> result = directory.Prepare(
                new TaskId(1),
                new FlowName("默认"),
                "目标");

            Assert.True(result.IsError);
            Assert.Equal(ErrorType.Failure, result.FirstError.Type);
            Assert.Equal(ErrorCodes.TaskWorkspaceCreate, result.FirstError.Code);
        }
        finally
        {
            File.Delete(root);
        }
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