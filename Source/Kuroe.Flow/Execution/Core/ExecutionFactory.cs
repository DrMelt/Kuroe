using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Flow.Execution;

/// <summary>执行实例的建立入口。状态迁移仍只由执行核完成。</summary>
internal static class ExecutionFactory
{
    /// <summary>建立一个排队中的执行。</summary>
    public static IExecution Create(ExecutionId id, ExecutionRequest request) => new Execution(id, request);

    /// <summary>按存档数据重建一次执行。</summary>
    public static IExecution Restore(ExecutionRestorePlan plan) =>
        Execution.Restore(
            plan.Id,
            plan.Request,
            plan.CreatedAt,
            plan.State,
            plan.Progress,
            plan.StartedAt,
            plan.FinishedAt,
            plan.Failures,
            plan.Usage,
            plan.RestoreData,
            plan.Contract,
            plan.ContinuedFrom);
}