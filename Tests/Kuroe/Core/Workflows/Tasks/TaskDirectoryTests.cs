using ErrorOr;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
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
    public void Unrelated_directories_are_ignored()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "0002-审阅-甲"));
        Directory.CreateDirectory(Path.Combine(_root, "0010-审阅-乙"));

        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        TaskId id = new(5);

        string prepared = directory.Prepare(id).ThrowIfError();

        Assert.Equal(Path.Combine(_root, id.Value), prepared);
        Assert.Equal(3, Directory.GetDirectories(_root).Length);
        Assert.Null(directory.Find(new TaskId(999)));
        Assert.Equal(prepared, directory.Find(id));
    }

    [Fact]
    public void An_occupied_name_reports_a_conflict()
    {
        TaskDirectory directory = TaskDirectory.Create(_root).Value;
        directory.Prepare(new TaskId(1)).ThrowIfError();

        ErrorOr<string> second = directory.Prepare(new TaskId(1));

        Assert.True(second.IsError);
        Assert.Equal(ErrorCodes.TaskWorkspaceConflict, second.FirstError.Code);
    }

    [Fact]
    public void A_root_occupied_by_a_file_reports_the_workspace_error()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        string? parent = Path.GetDirectoryName(root);
        Assert.NotNull(parent);
        Directory.CreateDirectory(parent);
        File.WriteAllText(root, "occupied");

        try
        {
            TaskDirectory directory = TaskDirectory.Create(root).Value;

            ErrorOr<string> result = directory.Prepare(new TaskId(1));

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