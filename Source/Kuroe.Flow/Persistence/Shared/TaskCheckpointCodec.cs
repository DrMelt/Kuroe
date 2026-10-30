using ErrorOr;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>检查点文本与文档之间的编解码端口。</summary>
public interface ITaskCheckpointCodec
{
    /// <summary>读取文本。格式不符或版本不支持时返回错误。</summary>
    ErrorOr<TaskCheckpointDocument> Read(string text);

    /// <summary>写出文本。输出必须稳定，同一文档重复写出的文本一致。</summary>
    ErrorOr<string> Write(TaskCheckpointDocument document);
}
