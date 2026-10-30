using ErrorOr;
using Kuroe.Flow.Persistence.FileSystem;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Flow.Persistence.Tests;

/// <summary>启动加载服务：空存储、逐文件失败与版本不支持的读取。</summary>
public sealed class TaskRestoreServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Restore_empty_store_returns_empty_report()
    {
        TaskLoadReport report = CreateService(CreateStore()).Restore();

        Assert.Empty(report.Loaded);
        Assert.Empty(report.Busy);
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void Restore_reports_unreadable_file()
    {
        ITaskCheckpointStore store = CreateStore();
        File.WriteAllText(Path.Combine(_root, "broken.json"), "{");

        TaskLoadReport report = CreateService(store).Restore();

        TaskLoadFailure failure = Assert.Single(report.Failures);
        Assert.Equal("broken.json", failure.Source);
    }

    [Fact]
    public void Restore_reports_unsupported_version()
    {
        FileTaskCheckpointStore store = CreateStore();
        Assert.False(store.Write(TaskCheckpointFixture.CreateDocument() with { Version = 999 }).IsError);

        TaskLoadReport report = CreateService(store).Restore();

        Assert.Empty(report.Loaded);
        TaskLoadFailure failure = Assert.Single(report.Failures);
        Assert.Contains(failure.Errors, error => error.Code == FlowErrorCodes.CheckpointVersion);
    }

    private FileTaskCheckpointStore CreateStore()
    {
        Directory.CreateDirectory(_root);

        return new FileTaskCheckpointStore(_root, new TaskCheckpointCodec());
    }

    private static TaskRestoreService CreateService(ITaskCheckpointStore store) =>
        new(
            store,
            new UnusedCompiler(),
            new UnusedLeaseStore(),
            new UnusedRuntime());

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

    /// <summary>这些场景不恢复任务。</summary>
    private sealed class UnusedRuntime : ITaskRestoreRuntime
    {
        public ErrorOr<TaskSnapshot> Restore(
            TaskCheckpoint checkpoint,
            CompiledFlow flow,
            IReadOnlyList<NodePath> seed) =>
            throw new InvalidOperationException("这些场景不应恢复任务。");
    }
}