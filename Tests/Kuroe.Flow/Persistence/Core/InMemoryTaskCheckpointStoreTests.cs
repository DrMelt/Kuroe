using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.TestSupport;

namespace Kuroe.Flow.Persistence.Tests;

/// <summary>用内存存储验证任务检查点存储契约。</summary>
public sealed class InMemoryTaskCheckpointStoreTests : TaskCheckpointStoreContractTests
{
    protected override ITaskCheckpointStore CreateStore() => new InMemoryTaskCheckpointStore();

    protected override void InjectReadFailure(ITaskCheckpointStore store, TaskStoreFailure failure) =>
        ((InMemoryTaskCheckpointStore)store).AddFailure(failure);
}
