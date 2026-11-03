using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Files.Tests;

/// <summary>文件存档存储的契约实现，失败注入只在测试侧包装。</summary>
public sealed class FileTaskRecordStoreTests : TaskRecordStoreContractTests, IDisposable
{
    private static readonly TaskRecordCodec Codec = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    protected override ITaskRecordStore CreateStore() =>
        new InjectableStore(new FileTaskRecordStore(_root, Codec));

    protected override void InjectReadFailure(ITaskRecordStore store, TaskStoreFailure failure) =>
        Assert.IsType<InjectableStore>(store).Inject(failure);

    [Fact]
    public void Read_reports_malformed_file()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, $"broken{Codec.FileExtension}");
        File.WriteAllText(file, "{");
        FileTaskRecordStore store = new(_root, Codec);

        TaskStoreReadResult result = store.ReadAll();

        TaskStoreFailure failure = Assert.Single(result.Failures);
        Assert.Equal(Path.GetFileName(file), failure.Source);
        Assert.Equal(FlowErrorCodes.RecordFormat, Assert.Single(failure.Errors).Code);
    }

    [Fact]
    public void Read_reports_unsupported_version()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, $"old{Codec.FileExtension}");
        File.WriteAllText(file, """{"Version":1,"Task":{}}""");
        FileTaskRecordStore store = new(_root, Codec);

        TaskStoreReadResult result = store.ReadAll();

        TaskStoreFailure failure = Assert.Single(result.Failures);
        Assert.Equal(Path.GetFileName(file), failure.Source);
        Assert.Equal(FlowErrorCodes.RecordVersion, Assert.Single(failure.Errors).Code);
    }

    [Fact]
    public void Read_ignores_files_with_other_extensions()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "ignored.txt"), "不是存档");
        FileTaskRecordStore store = new(_root, Codec);

        TaskStoreReadResult result = store.ReadAll();

        Assert.Empty(result.Records);
        Assert.Empty(result.Failures);
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
