using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Persistence.FileSystem;
using Xunit;

namespace Kuroe.Tests.Persistence;

/// <summary>检查点文件随任务清理一起删除。</summary>
public sealed class TaskCheckpointCleanupTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);

    [Fact]
    public void Clear_settled_tasks_deletes_checkpoints()
    {
        TaskId id = _environment.Submit("清理任务");
        _environment.Settle(id);
        string directory = Path.Combine(_environment.Root, ".kuroe_tasks", ".checkpoints");
        FileTaskCheckpointStore store = new(directory, new TaskCheckpointCodec());
        WaitForCompleted(store, id);

        _environment.Tasks.ClearSettled();

        Assert.DoesNotContain(store.ReadAll().Documents, document => document.Task.Identity.Id == id);
        Assert.False(File.Exists(Path.Combine(directory, $"{id.Value}.json")));
    }

    /// <summary>等完成事实落到检查点，避免清理与终态写盘交叠。</summary>
    private static void WaitForCompleted(FileTaskCheckpointStore store, TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (store.ReadAll().Documents.Any(document => document.Task.Identity.Id == id && document.Task.Completed))
            {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail($"任务 {id} 的终态检查点没有写入。");
    }

    public void Dispose() => _environment.Dispose();
}