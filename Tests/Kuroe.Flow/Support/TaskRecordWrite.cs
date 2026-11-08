using Kuroe.Flow.Persistence.Records.Shared;

namespace Kuroe.Flow.TestSupport;

/// <summary>一次存档写入：写入序号与当时的存档内容。</summary>
public readonly record struct TaskRecordWrite(int Sequence, TaskRecord Record);