using ErrorOr;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Host.Platform;
using Kuroe.Host.Composition.Tests.Support;
using Kuroe.Host.FlowIntegration.GraphFlow.HostInputs;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Host.Composition.Tests.Workflows.Tasks;

/// <summary>任务工作目录的命名与建立。</summary>
public sealed class TaskDirectoryTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact(DisplayName = "无关目录被忽略")]
    public void Unrelated_directories_are_ignored()
    {
        _files.AddDirectory(_root);
        _files.AddDirectory(Path.Combine(_root, "0002-审阅-甲"));
        _files.AddDirectory(Path.Combine(_root, "0010-审阅-乙"));

        TaskDirectory directory = TaskDirectory.Create(_root, _files).Value;
        TaskId id = new(5);

        string prepared = directory.Prepare(id).ThrowIfError();

        Assert.Equal(Path.Combine(_root, id.Value), prepared);
        Assert.Equal(3, _files.Directories(_root).Count);
        Assert.Null(directory.Find(new TaskId(999)));
        Assert.Equal(prepared, directory.Find(id));
    }

    [Fact(DisplayName = "名称被占用时报告冲突")]
    public void An_occupied_name_reports_a_conflict()
    {
        TaskDirectory directory = TaskDirectory.Create(_root, _files).Value;
        directory.Prepare(new TaskId(1)).ThrowIfError();

        ErrorOr<string> second = directory.Prepare(new TaskId(1));

        Assert.True(second.IsError);
        Assert.Equal(ErrorCodes.TaskWorkspaceConflict, second.FirstError.Code);
    }

    [Fact(DisplayName = "根被文件占用时报告工作区错误")]
    public void A_root_occupied_by_a_file_reports_the_workspace_error()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        _files.AddFile(root, "occupied");

        TaskDirectory directory = TaskDirectory.Create(root, _files).Value;

        ErrorOr<string> result = directory.Prepare(new TaskId(1));

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Failure, result.FirstError.Type);
        Assert.Equal(ErrorCodes.TaskWorkspaceCreate, result.FirstError.Code);
    }
}