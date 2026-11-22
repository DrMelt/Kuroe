using Kuroe.Files.Shared;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Composition.Pending;
using Kuroe.Kernel.Tasks.Persistence.Files;
using Kuroe.Kernel.Tasks.Persistence.Files.Shared;
using Kuroe.Kernel.Tasks.Persistence.Restoring;
using Kuroe.Kernel.Tasks.Persistence.Restoring.Shared;
using Kuroe.Kernel.Tasks.Persistence.Saving;
using Kuroe.Kernel.Tasks.Persistence.Saving.Shared;
using Kuroe.Kernel.Tasks.Persistence.Serialization.Shared;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Runtime.Persistence;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Composition.Tasks;

/// <summary>一个任务域的装配产物：任务索引、输入等待、任务动作与恢复服务。</summary>
public sealed record TaskDomain(
    TaskRegistry Registry,
    PendingService Pendings,
    TaskActions Actions,
    ITaskRecordCoordinator? Records,
    ITaskRestoreService? Restore);

/// <summary>任务域装配：按存档目录建立独立的注册表、存档、租约、写盘协调与恢复服务。</summary>
public static class TaskDomainComposition
{
    /// <summary>建立一个任务域。不写存档时不建立存档与恢复。租约按存档目录经工厂建立。</summary>
    public static TaskDomain Create(
        TaskRegistry registry,
        IExecutionRuntime executions,
        IFlowRuntime flow,
        IFlowProviderSource providers,
        ITaskIdSource ids,
        TimeProvider time,
        bool persist,
        string? recordsDirectory,
        ITaskRecordCodec? codec,
        IWorkspaceFiles files,
        Func<string, ITaskLeaseStore> leaseStores)
    {
        PendingService pendings = new(registry);
        if (!persist)
        {
            return new TaskDomain(
                registry,
                pendings,
                new TaskActions(registry, executions, flow, providers, ids, time),
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(recordsDirectory) || codec is null)
        {
            throw new InvalidOperationException("写任务存档需要存档目录与存档编解码。");
        }

        ITaskRecordStore records = new FileTaskRecordStore(recordsDirectory, codec, files);
        ITaskLeaseStore leases = leaseStores(recordsDirectory);
        TaskRecordCoordinator coordinator = new(new TaskRecordSource(registry), records);
        TaskRestoreRuntime restoreRuntime = new(registry, executions, providers, flow, time);

        return new TaskDomain(
            registry,
            pendings,
            new TaskActions(registry, executions, flow, providers, ids, time, coordinator, leases),
            coordinator,
            new TaskRestoreService(records, leases, restoreRuntime));
    }
}