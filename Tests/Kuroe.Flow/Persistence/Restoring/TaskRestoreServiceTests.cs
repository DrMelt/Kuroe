using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
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
        TaskRestoreService service = new(
            store,
            new FailingCompiler(),
            new SingleLeaseStore(),
            new UnusedRuntime());

        TaskLoadReport report = service.Restore();

        TaskLoadFailure failure = Assert.Single(report.Failures);
        Assert.Equal(record.Identity.Id, failure.Task);
        Assert.Equal(record.Identity.Id.Value, failure.Source);
    }

    private static TaskRestoreService CreateService(ITaskRecordStore store) =>
        new(
            store,
            new UnusedCompiler(),
            new UnusedLeaseStore(),
            new UnusedRuntime());

    /// <summary>这些场景不编译流程快照。</summary>
    private sealed class UnusedCompiler : IFlowSnapshotCompiler
    {
        public ErrorOr<CompiledFlow> Compile(FlowSnapshot snapshot) =>
            throw new InvalidOperationException("这些场景不应编译流程快照。");
    }

    /// <summary>这些场景不获取租约。</summary>
    private sealed class UnusedLeaseStore : ITaskLeaseStore
    {
        public ErrorOr<ITaskLease> TryAcquire(TaskId id) =>
            throw new InvalidOperationException("这些场景不应获取租约。");
    }

    /// <summary>编译一定失败，用于验证失败来源。</summary>
    private sealed class FailingCompiler : IFlowSnapshotCompiler
    {
        public ErrorOr<CompiledFlow> Compile(FlowSnapshot snapshot) =>
            [Error.Failure("Test.Compile", "编译失败。")];
    }

    /// <summary>发放一个不占用外部资源的租约。</summary>
    private sealed class SingleLeaseStore : ITaskLeaseStore
    {
        public ErrorOr<ITaskLease> TryAcquire(TaskId id) => new StubLease(id);

        private sealed class StubLease(TaskId id) : ITaskLease
        {
            public TaskId Id => id;

            public void Dispose()
            {
            }
        }
    }

    /// <summary>这些场景不恢复任务。</summary>
    private sealed class UnusedRuntime : ITaskRestoreRuntime
    {
        public ErrorOr<TaskSnapshot> Restore(
            TaskRecord record,
            CompiledFlow flow,
            IReadOnlyList<NodePath> seed) =>
            throw new InvalidOperationException("这些场景不应恢复任务。");
    }
}