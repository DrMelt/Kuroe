using Kuroe.Kernel.Tasks.Persistence.Serialization.Json;
using Kuroe.Kernel.Tasks.Persistence.Files;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>存档文件随任务清理一起删除。</summary>
public sealed class TaskRecordCleanupTests : IDisposable
{
    private static readonly TaskRecordCodec Codec = Kuroe.Flow.TestSupport.TaskRecordCodecs.Codec();
    private readonly TestEnvironment _environment = TestEnvironment.Create(persistTasks: true);

    [Fact(DisplayName = "清理已收口任务删除存档")]
    public async Task Clear_settled_tasks_deletes_records()
    {
        TaskId id = _environment.Submit("清理任务");
        await _environment.SettleAsync(id);
        string directory = Path.Combine(_environment.Root, ".kuroe_tasks", ".records");
        FileTaskRecordStore store = new(directory, Codec);
        await _environment.Records.FlushAsync(id);
        Assert.Contains(store.ReadAll().Records, record => record.Identity.Id == id);

        _environment.Tasks.ClearSettled().ThrowIfError();

        Assert.DoesNotContain(store.ReadAll().Records, record => record.Identity.Id == id);
        string recordFile = Path.Combine(directory, $"{id.Value}{Codec.FileExtension}");
        Assert.False(File.Exists(recordFile), $"{recordFile} 不应存在。");
    }

    public void Dispose() => _environment.Dispose();
}