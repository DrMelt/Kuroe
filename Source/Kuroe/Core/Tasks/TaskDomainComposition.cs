using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Pending;
using Kuroe.Tasks.Persistence.Files;
using Kuroe.Tasks.Persistence.Files.Shared;
using Kuroe.Tasks.Persistence.Restoring;
using Kuroe.Tasks.Persistence.Restoring.Shared;
using Kuroe.Tasks.Persistence.Saving;
using Kuroe.Tasks.Persistence.Saving.Shared;
using Kuroe.Tasks.Persistence.Serialization.Shared;
using Kuroe.Tasks.Runtime;
using Kuroe.Tasks.Runtime.Persistence;
using Kuroe.Tasks.Shared;

namespace Kuroe;

/// <summary>一个任务域的装配产物：任务索引、输入等待、任务动作与恢复服务。</summary>
public sealed record TaskDomain(
    TaskRegistry Registry,
    PendingService Pendings,
    TaskActions Actions,
    ITaskRecordCoordinator? Records,
    ITaskRestoreService? Restore);

/// <summary>任务域装配：按存档目录建立独立的注册表、存档、租约、写盘协调与恢复服务，供用户任务域与内置助手会话域共用。</summary>
public static class TaskDomainComposition
{
    /// <summary>建立一个任务域。不写存档时不建立存档与恢复。</summary>
    public static TaskDomain Create(
        TaskRegistry registry,
        IExecutionRuntime executions,
        IFlowRuntime flow,
        IFlowProviderSource providers,
        ITaskIdSource ids,
        bool persist,
        string? recordsDirectory,
        ITaskRecordCodec? codec)
    {
        PendingService pendings = new(registry);
        if (!persist)
        {
            return new TaskDomain(
                registry,
                pendings,
                new TaskActions(registry, executions, flow, providers, ids),
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(recordsDirectory) || codec is null)
        {
            throw new InvalidOperationException("写任务存档需要存档目录与存档编解码。");
        }

        ITaskRecordStore records = new FileTaskRecordStore(recordsDirectory, codec);
        ITaskLeaseStore leases = new FileTaskLeaseStore(recordsDirectory);
        TaskRecordCoordinator coordinator = new(new TaskRecordSource(registry), records);
        TaskRestoreRuntime restoreRuntime = new(registry, executions, providers, flow);

        return new TaskDomain(
            registry,
            pendings,
            new TaskActions(registry, executions, flow, providers, ids, coordinator, leases),
            coordinator,
            new TaskRestoreService(records, leases, restoreRuntime));
    }
}