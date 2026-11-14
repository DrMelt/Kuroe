using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Tasks.Records.Shared;

namespace Kuroe.Flow.TestSupport;

/// <summary>会记录写入事实的存档存储：观察方等满足条件的写入发生。</summary>
public interface ITaskRecordRecordingStore : ITaskRecordStore
{
    /// <summary>已发生的写入，按发生顺序。</summary>
    IReadOnlyList<TaskRecordWrite> Writes { get; }

    /// <summary>等到满足条件的写入发生，已发生时立即返回。</summary>
    Task<TaskRecordWrite> NextWrite(Func<TaskRecord, bool> condition);
}