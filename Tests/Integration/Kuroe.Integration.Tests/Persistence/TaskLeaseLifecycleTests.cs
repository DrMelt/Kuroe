using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Kernel.Tasks.Persistence.Files;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>任务租约随任务生命周期获取与释放：创建即持有，终止与清理后放开。</summary>
public sealed class TaskLeaseLifecycleTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(TestFlows.InputFlow, persistTasks: true);

    [Fact(DisplayName = "提交后持有租约直到任务停止")]
    public void Submit_holds_the_lease_until_the_task_is_stopped()
    {
        using FileTaskLeaseStore probe = new(KuroePaths.At(_environment.Root).RecordsDirectory);
        TaskId id = _environment.Submit("占用任务");

        ErrorOr<Success> held = probe.TryAcquire(id);

        Assert.True(held.IsError);
        Assert.Equal(ErrorType.Conflict, held.FirstError.Type);

        _environment.Tasks.StopTask(id).ThrowIfError();

        probe.TryAcquire(id).ThrowIfError();
    }

    [Fact(DisplayName = "清理已收口任务释放租约")]
    public void Clear_settled_releases_the_lease()
    {
        using FileTaskLeaseStore probe = new(KuroePaths.At(_environment.Root).RecordsDirectory);
        TaskId id = _environment.Submit("完成占用");
        _environment.Wait(id, _ => _environment.Awaiting(id));
        _environment.Pendings.Answer(id, null, "回答").ThrowIfError();
        _environment.Settle(id);

        Assert.True(probe.TryAcquire(id).IsError);

        _environment.Tasks.ClearSettled().ThrowIfError();

        probe.TryAcquire(id).ThrowIfError();
    }

    [Fact(DisplayName = "关闭释放租约")]
    public async Task Shutdown_releases_the_lease()
    {
        using FileTaskLeaseStore probe = new(KuroePaths.At(_environment.Root).RecordsDirectory);
        TaskId id = _environment.Submit("退出占用");

        Assert.True(probe.TryAcquire(id).IsError);

        await _environment.Tasks.ShutdownAsync();

        probe.TryAcquire(id).ThrowIfError();
    }

    public void Dispose() => _environment.Dispose();
}