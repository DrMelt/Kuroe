using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.FileSystem.Tests;

/// <summary>任务租约的文件实现：独占、释放与锁文件保留。</summary>
public sealed class FileTaskLeaseStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Acquire_then_release_keeps_the_lock_file()
    {
        FileTaskLeaseStore store = new(_root);
        TaskId id = new(7);

        using (store.TryAcquire(id).ThrowIfError())
        {
            Assert.True(File.Exists(LockFile(id)));
        }

        Assert.True(File.Exists(LockFile(id)));
        using ITaskLease again = store.TryAcquire(id).ThrowIfError();
        Assert.Equal(id, again.Id);
    }

    [Fact]
    public void Another_store_cannot_acquire_a_held_lease()
    {
        FileTaskLeaseStore first = new(_root);
        FileTaskLeaseStore second = new(_root);
        TaskId id = new(8);

        using ITaskLease held = first.TryAcquire(id).ThrowIfError();
        ErrorOr<ITaskLease> busy = second.TryAcquire(id);

        Assert.True(busy.IsError);
        Assert.Equal(FlowErrorCodes.TaskLease, busy.FirstError.Code);

        held.Dispose();
        using ITaskLease taken = second.TryAcquire(id).ThrowIfError();
    }

    [Fact]
    public void Same_store_rejects_a_second_lease()
    {
        FileTaskLeaseStore store = new(_root);
        TaskId id = new(9);

        using ITaskLease first = store.TryAcquire(id).ThrowIfError();
        ErrorOr<ITaskLease> second = store.TryAcquire(id);

        Assert.True(second.IsError);
        Assert.Equal(FlowErrorCodes.TaskLease, second.FirstError.Code);
    }

    [Fact]
    public void Different_tasks_do_not_block_each_other()
    {
        FileTaskLeaseStore store = new(_root);

        using ITaskLease first = store.TryAcquire(new TaskId(10)).ThrowIfError();
        using ITaskLease second = store.TryAcquire(new TaskId(11)).ThrowIfError();

        Assert.Equal(new TaskId(10), first.Id);
        Assert.Equal(new TaskId(11), second.Id);
    }

    private string LockFile(TaskId id) => Path.Combine(_root, $"{id.Value}.lock");

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
