namespace Kuroe;

/// <summary>核心装配选项：任务持久化开关。</summary>
public sealed record KuroeCoreOptions
{
    /// <summary>任务是否写存档并占用租约。开启后宿主可调用恢复服务加载存档。</summary>
    public bool PersistTasks { get; init; } = true;
}