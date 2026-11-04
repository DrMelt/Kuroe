namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>存档格式版本。读取接受 MinimumReadable 到 Current 的版本，写入始终使用当前版本。</summary>
public static class TaskRecordSerializationVersion
{
    /// <summary>当前版本。</summary>
    public const int Current = 6;

    /// <summary>可读取的最早版本。</summary>
    public const int MinimumReadable = 5;

    /// <summary>节点阻塞原因首次进入存档的版本。</summary>
    public const int BlockIntroduced = 6;

    /// <summary>给定版本是否可读取。</summary>
    public static bool IsReadable(int version) => version is >= MinimumReadable and <= Current;
}
