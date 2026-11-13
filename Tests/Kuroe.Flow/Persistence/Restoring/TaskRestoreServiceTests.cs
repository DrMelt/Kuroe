using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Restoring.Tests;

/// <summary>启动加载服务：空存储与存储读取失败的转发。</summary>
public sealed class TaskRestoreServiceTests
{
    [Fact]
    public void Restore_empty_store_returns_empty_report()
    {
        TaskLoadReport report = CreateService(new InMemoryTaskRecordStore()).Restore();

        Assert.Empty(report.Loaded);
        Assert.Empty(report.Busy);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void Restore_reports_store_failure()
    {
        InMemoryTaskRecordStore store = new();
        store.AddFailure(new TaskStoreFailure(
            "broken.json",
            [Error.Failure(FlowErrorCodes.RecordVersion, "版本不支持。")]));

        TaskLoadReport report = CreateService(store).Restore();

        Assert.Empty(report.Loaded);
        TaskLoadFailure failure = Assert.Single(report.Failures);
        Assert.Null(failure.Task);
        Assert.Equal("broken.json", failure.Source);
        Assert.Contains(failure.Errors, error => error.Code == FlowErrorCodes.RecordVersion);
    }

    [Fact]
    public void Restore_failure_reports_task_identity()
    {
        InMemoryTaskRecordStore store = new();
        TaskRecord record = TaskRecordFixture.Create();
        store.Write(record).ThrowIfError();
        SingleLeaseStore leases = new();
        TaskRestoreService service = new(
            store,
            leases,
            new UnusedRuntime());

        TaskLoadReport report = service.Restore();

        TaskLoadFailure failure = Assert.Single(report.Failures);
        Assert.Equal(record.Identity.Id, failure.Task);
        Assert.Equal(record.Identity.Id.Value, failure.Source);
        Assert.Contains(record.Identity.Id, leases.Released);
    }

    [Fact]
    public void Restore_reports_busy_when_the_lease_is_held()
    {
        InMemoryTaskRecordStore store = new();
        TaskRecord record = TaskRecordFixture.Create();
        store.Write(record).ThrowIfError();
        TaskRestoreService service = new(
            store,
            new BusyLeaseStore(),
            new UnusedRuntime());

        TaskLoadReport report = service.Restore();

        Assert.Empty(report.Loaded);
        Assert.Contains(record.Identity.Id, report.Busy);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void Restore_reports_lease_failure_as_task_failure()
    {
        InMemoryTaskRecordStore store = new();
        TaskRecord record = TaskRecordFixture.Create();
        store.Write(record).ThrowIfError();
        TaskRestoreService service = new(
            store,
            new BrokenLeaseStore(),
            new UnusedRuntime());

        TaskLoadReport report = service.Restore();

        Assert.Empty(report.Loaded);
        Assert.Empty(report.Busy);
        TaskLoadFailure failure = Assert.Single(report.Failures);
        Assert.Equal(record.Identity.Id, failure.Task);
        Assert.Contains(failure.Errors, error => error.Code == FlowErrorCodes.TaskLease);
    }

    private static TaskRestoreService CreateService(ITaskRecordStore store) =>
        new(
            store,
            new UnusedLeaseStore(),
            new UnusedRuntime());

    /// <summary>这些场景不获取租约。</summary>
    private sealed class UnusedLeaseStore : ITaskLeaseStore
    {
        public ErrorOr<Success> TryAcquire(TaskId id) =>
            throw new InvalidOperationException("这些场景不应获取租约。");

        public void Release(TaskId id) =>
            throw new InvalidOperationException("这些场景不应释放租约。");

        public void ReleaseAll() =>
            throw new InvalidOperationException("这些场景不应释放租约。");
    }

    /// <summary>发放租约并记录注销，用于验证恢复失败后的清理。</summary>
    private sealed class SingleLeaseStore : ITaskLeaseStore
    {
        public List<TaskId> Released { get; } = [];

        public ErrorOr<Success> TryAcquire(TaskId id) => Result.Success;

        public void Release(TaskId id) => Released.Add(id);

        public void ReleaseAll() => Released.Clear();
    }

    /// <summary>租约由其他进程持有。</summary>
    private sealed class BusyLeaseStore : ITaskLeaseStore
    {
        public ErrorOr<Success> TryAcquire(TaskId id) =>
            [Error.Conflict(FlowErrorCodes.TaskLease, "占用。")];

        public void Release(TaskId id)
        {
        }

        public void ReleaseAll()
        {
        }
    }

    /// <summary>租约建立失败，不是占用。</summary>
    private sealed class BrokenLeaseStore : ITaskLeaseStore
    {
        public ErrorOr<Success> TryAcquire(TaskId id) =>
            [Error.Failure(FlowErrorCodes.TaskLease, "目录不可写。")];

        public void Release(TaskId id)
        {
        }

        public void ReleaseAll()
        {
        }
    }

    /// <summary>这些场景不恢复任务。</summary>
    private sealed class UnusedRuntime : ITaskRestoreRuntime
    {
        public ErrorOr<TaskSnapshot> Restore(TaskRecord record) =>
            throw new InvalidOperationException("这些场景不应恢复任务。");
    }
}