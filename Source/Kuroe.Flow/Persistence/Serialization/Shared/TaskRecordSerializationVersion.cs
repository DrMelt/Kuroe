namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>存档格式版本。读取当前版本，迁移上一版本，写入始终使用当前版本。</summary>
public static class TaskRecordSerializationVersion
{
    /// <summary>当前版本。</summary>
    public const int Current = 14;

    /// <summary>给定版本是否为当前版本。</summary>
    public static bool IsReadable(int version) => version == Current;

    /// <summary>给定版本是否可由当前版本迁移读取。</summary>
    public static bool IsMigratable(int version) => version == Current - 1;
}
