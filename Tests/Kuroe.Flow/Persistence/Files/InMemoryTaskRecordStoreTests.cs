using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.TestSupport;

namespace Kuroe.Flow.Persistence.Files.Tests;

/// <summary>用内存存储验证任务存档存储契约。</summary>
public sealed class InMemoryTaskRecordStoreTests : TaskRecordStoreContractTests
{
    protected override ITaskRecordStore CreateStore() => new InMemoryTaskRecordStore();

    protected override void InjectReadFailure(ITaskRecordStore store, TaskStoreFailure failure) =>
        ((InMemoryTaskRecordStore)store).AddFailure(failure);
}
