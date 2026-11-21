using ErrorOr;
using Kuroe.Kernel.Tasks.Persistence.Files.Shared;
using Kuroe.Kernel.Tasks.Persistence.Serialization.Json;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Flow.TestSupport;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Kernel.Tasks.Persistence.Files.Tests;

/// <summary>文件存档存储的契约实现，失败注入只在测试侧包装。</summary>
public sealed class FileTaskRecordStoreTests : TaskRecordStoreContractTests
{
    private static readonly TaskRecordCodec Codec = TaskRecordCodecs.Codec();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    protected override ITaskRecordStore CreateStore() =>
        new InjectableStore(new FileTaskRecordStore(_root, Codec, _files));

    protected override void InjectReadFailure(ITaskRecordStore store, TaskStoreFailure failure) =>
        Assert.IsType<InjectableStore>(store).Inject(failure);

    [Fact(DisplayName = "读取报告格式错误的文件")]
    public void Read_reports_malformed_file()
    {
        string file = Path.Combine(_root, $"broken{Codec.FileExtension}");
        _files.AddFile(file, "{");
        FileTaskRecordStore store = new(_root, Codec, _files);

        TaskStoreReadResult result = store.ReadAll();

        TaskStoreFailure failure = Assert.Single(result.Failures);
        Assert.Equal(Path.GetFileName(file), failure.Source);
        Assert.Equal(FlowErrorCodes.RecordFormat, Assert.Single(failure.Errors).Code);
    }

    [Fact(DisplayName = "读取报告不支持的版本")]
    public void Read_reports_unsupported_version()
    {
        string file = Path.Combine(_root, $"old{Codec.FileExtension}");
        _files.AddFile(file, """{"Version":1,"Task":{}}""");
        FileTaskRecordStore store = new(_root, Codec, _files);

        TaskStoreReadResult result = store.ReadAll();

        TaskStoreFailure failure = Assert.Single(result.Failures);
        Assert.Equal(Path.GetFileName(file), failure.Source);
        Assert.Equal(FlowErrorCodes.RecordVersion, Assert.Single(failure.Errors).Code);
    }

    [Fact(DisplayName = "读取忽略其他扩展名的文件")]
    public void Read_ignores_files_with_other_extensions()
    {
        _files.AddFile(Path.Combine(_root, "ignored.txt"), "不是存档");
        FileTaskRecordStore store = new(_root, Codec, _files);

        TaskStoreReadResult result = store.ReadAll();

        Assert.Empty(result.Records);
        Assert.Empty(result.Failures);
    }

    private sealed class InjectableStore(ITaskRecordStore inner) : ITaskRecordStore
    {
        private readonly List<TaskStoreFailure> _failures = [];

        public void Inject(TaskStoreFailure failure) => _failures.Add(failure);

        public TaskStoreReadResult ReadAll()
        {
            TaskStoreReadResult result = inner.ReadAll();

            return new TaskStoreReadResult(result.Records, [.. result.Failures, .. _failures]);
        }

        public ErrorOr<Success> Write(TaskRecord record) => inner.Write(record);

        public ErrorOr<Success> Delete(TaskId id) => inner.Delete(id);
    }
}
