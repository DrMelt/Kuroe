using ErrorOr;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.FileSystem.Tests;

/// <summary>文件检查点存储的契约实现，失败注入只在测试侧包装。</summary>
public sealed class FileTaskCheckpointStoreTests : TaskCheckpointStoreContractTests, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    protected override ITaskCheckpointStore CreateStore() =>
        new InjectableStore(new FileTaskCheckpointStore(_root, new TaskCheckpointCodec()));

    protected override void InjectReadFailure(ITaskCheckpointStore store, TaskStoreFailure failure) =>
        Assert.IsType<InjectableStore>(store).Inject(failure);

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

    private sealed class InjectableStore(ITaskCheckpointStore inner) : ITaskCheckpointStore
    {
        private readonly List<TaskStoreFailure> _failures = [];

        public void Inject(TaskStoreFailure failure) => _failures.Add(failure);

        public TaskStoreReadResult ReadAll()
        {
            TaskStoreReadResult result = inner.ReadAll();

            return new TaskStoreReadResult(result.Documents, [.. result.Failures, .. _failures]);
        }

        public ErrorOr<Success> Write(TaskCheckpointDocument document) => inner.Write(document);

        public ErrorOr<Success> Delete(TaskId id) => inner.Delete(id);
    }
}
