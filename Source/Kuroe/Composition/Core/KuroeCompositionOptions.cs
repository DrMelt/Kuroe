using Kuroe.Files.Shared;
using Kuroe.Kernel.Tasks.Persistence.Files.Shared;

namespace Kuroe.Composition;

/// <summary>装配选项：任务持久化开关、文件访问实现与租约工厂。</summary>
public sealed record KuroeCompositionOptions
{
    /// <summary>任务是否写存档并占用租约。开启后宿主可调用恢复服务加载存档。</summary>
    public bool PersistTasks { get; init; } = true;

    /// <summary>文件访问实现，未给出时使用系统文件访问。</summary>
    public IWorkspaceFiles? Files { get; init; }

    /// <summary>按存档目录建立任务租约的工厂，未给出时使用文件租约。</summary>
    public Func<string, ITaskLeaseStore>? LeaseStores { get; init; }
}