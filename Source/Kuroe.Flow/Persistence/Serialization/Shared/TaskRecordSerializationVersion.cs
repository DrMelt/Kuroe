namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>存档格式版本。只读取当前版本，写入始终使用当前版本。</summary>
public static class TaskRecordSerializationVersion
{
    /// <summary>当前版本。</summary>
    public const int Current = 13;

    /// <summary>给定版本是否可读取。</summary>
    public static bool IsReadable(int version) => version == Current;
}
