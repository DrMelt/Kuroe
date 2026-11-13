using ErrorOr;
using Kuroe.Flow.Tasks.Records.Shared;

namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>存档文本与任务存档之间的编解码端口。不需要文本的实现直接实现存储端口。</summary>
public interface ITaskRecordCodec
{
    /// <summary>存档文件的扩展名，含前导点。</summary>
    string FileExtension { get; }

    /// <summary>读取文本。格式不符或版本不支持时返回错误。</summary>
    ErrorOr<TaskRecord> Read(string text);

    /// <summary>写出文本。输出必须稳定，同一存档重复写出的文本一致。</summary>
    ErrorOr<string> Write(TaskRecord record);
}
