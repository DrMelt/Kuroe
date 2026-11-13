using ErrorOr;
using Kuroe.Flow.Tasks.Shared;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Files.Tests;

/// <summary>任务租约的文件实现：独占、释放与锁文件保留。</summary>
public sealed class FileTaskLeaseStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Acquire_then_release_keeps_the_lock_file()
    {
        using FileTaskLeaseStore store = new(_root);
        TaskId id = new(7);

        store.TryAcquire(id).ThrowIfError();
        Assert.True(File.Exists(LockFile(id)));

        store.Release(id);
        Assert.True(File.Exists(LockFile(id)));
        store.TryAcquire(id).ThrowIfError();
    }

    [Fact]
    public void Leftover_lock_file_does_not_block_acquisition()
    {
        using FileTaskLeaseStore store = new(_root);
        TaskId id = new(12);
        Directory.CreateDirectory(_root);
        File.WriteAllText(LockFile(id), string.Empty);

        store.TryAcquire(id).ThrowIfError();

        Assert.True(File.Exists(LockFile(id)));
    }

    [Fact]
    public void Another_store_cannot_acquire_a_held_lease()
    {
        using FileTaskLeaseStore first = new(_root);
        using FileTaskLeaseStore second = new(_root);
        TaskId id = new(8);

        first.TryAcquire(id).ThrowIfError();
        ErrorOr<Success> busy = second.TryAcquire(id);

        Assert.True(busy.IsError);
        Assert.Equal(FlowErrorCodes.TaskLease, busy.FirstError.Code);
        Assert.Equal(ErrorType.Conflict, busy.FirstError.Type);

        first.Release(id);
        second.TryAcquire(id).ThrowIfError();
    }

    [Fact]
    public void Same_store_rejects_a_second_lease()
    {
        using FileTaskLeaseStore store = new(_root);
        TaskId id = new(9);

        store.TryAcquire(id).ThrowIfError();
        ErrorOr<Success> second = store.TryAcquire(id);

        Assert.True(second.IsError);
        Assert.Equal(FlowErrorCodes.TaskLease, second.FirstError.Code);
        Assert.Equal(ErrorType.Conflict, second.FirstError.Type);
    }

    [Fact]
    public void Different_tasks_do_not_block_each_other()
    {
        using FileTaskLeaseStore store = new(_root);

        Assert.False(store.TryAcquire(new TaskId(10)).IsError);
        Assert.False(store.TryAcquire(new TaskId(11)).IsError);
    }

    [Fact]
    public void ReleaseAll_frees_every_task()
    {
        using FileTaskLeaseStore first = new(_root);
        using FileTaskLeaseStore second = new(_root);
        TaskId one = new(14);
        TaskId two = new(15);

        first.TryAcquire(one).ThrowIfError();
        first.TryAcquire(two).ThrowIfError();
        first.ReleaseAll();

        Assert.False(second.TryAcquire(one).IsError);
        Assert.False(second.TryAcquire(two).IsError);
    }

    [Fact]
    public void Release_of_an_unheld_task_is_ignored()
    {
        using FileTaskLeaseStore store = new(_root);
        TaskId id = new(16);

        store.Release(id);

        Assert.False(store.TryAcquire(id).IsError);
    }

    [Fact]
    public void Unusable_directory_reports_failure()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "file");
        File.WriteAllText(path, string.Empty);
        using FileTaskLeaseStore store = new(path);

        ErrorOr<Success> acquired = store.TryAcquire(new TaskId(13));

        Assert.True(acquired.IsError);
        Assert.Equal(FlowErrorCodes.TaskLease, acquired.FirstError.Code);
        Assert.Equal(ErrorType.Failure, acquired.FirstError.Type);
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
