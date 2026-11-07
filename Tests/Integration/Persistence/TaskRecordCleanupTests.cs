using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Persistence.Files;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>存档文件随任务清理一起删除。</summary>
public sealed class TaskRecordCleanupTests : IDisposable
{
    private static readonly TaskRecordCodec Codec = new();
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);

    [Fact]
    public async Task Clear_settled_tasks_deletes_records()
    {
        TaskId id = _environment.Submit("清理任务");
        await _environment.SettleAsync(id);
        await _environment.Tasks.FlushAsync();
        string directory = Path.Combine(_environment.Root, ".kuroe_tasks", ".records");
        FileTaskRecordStore store = new(directory, Codec);
        WaitForCompleted(store, id);

        _environment.Tasks.ClearSettled();

        Assert.DoesNotContain(store.ReadAll().Records, record => record.Identity.Id == id);
        Assert.False(File.Exists(Path.Combine(directory, $"{id.Value}{Codec.FileExtension}")));
    }

    /// <summary>等完成事实落到存档，避免清理与终态写盘交叠。</summary>
    private static void WaitForCompleted(FileTaskRecordStore store, TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (store.ReadAll().Records.Any(record => record.Identity.Id == id && record.Completed))
            {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail($"任务 {id} 的终态存档没有写入。");
    }

    public void Dispose() => _environment.Dispose();
}