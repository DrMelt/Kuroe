using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Persistence.Files;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>存档文件随任务清理一起删除。</summary>
public sealed class TaskRecordCleanupTests : IDisposable
{
    private static readonly TaskRecordCodec Codec = Kuroe.Flow.TestSupport.TaskRecordCodecs.Codec();
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);

    [Fact]
    public async Task Clear_settled_tasks_deletes_records()
    {
        TaskId id = _environment.Submit("清理任务");
        await _environment.SettleAsync(id);
        string directory = Path.Combine(_environment.Root, ".kuroe_tasks", ".records");
        FileTaskRecordStore store = new(directory, Codec);
        await _environment.Records.FlushAsync(id);
        Assert.Contains(store.ReadAll().Records, record => record.Identity.Id == id);

        _environment.Tasks.ClearSettled();

        Assert.DoesNotContain(store.ReadAll().Records, record => record.Identity.Id == id);
        Assert.False(File.Exists(Path.Combine(directory, $"{id.Value}{Codec.FileExtension}")));
    }

    public void Dispose() => _environment.Dispose();
}