using ErrorOr;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution;

/// <summary>一次执行的包裹：持有执行实例与运行器，负责运行器接线、执行与收口。</summary>
internal sealed class ExecutionRun(
    ExecutionInstance execution,
    IExecutionRunner runner)
{
    /// <summary>本次执行的实例。</summary>
    public ExecutionInstance Execution { get; } = execution;

    /// <summary>建立请求入口、运行器并包裹为一次执行。</summary>
    public static ExecutionRun Create(
        ExecutionInstance execution,
        ExecutionInvocation invocation)
    {
        ExecutionRequestEntry requests = new(execution);

        return new(execution, invocation.Factory.Create(invocation.Parameters, requests));
    }

    /// <summary>按恢复结果建立请求入口、运行器并包裹为一次执行。</summary>
    public static ExecutionRun Restore(
        ExecutionInstance execution,
        ExecutionInvocation invocation,
        ExecutionRestoreContext context,
        IExecutionRestoreData data)
    {
        IExecutionRestorer restorer = invocation.Factory as IExecutionRestorer
            ?? throw new InvalidOperationException("执行器工厂不支持恢复。");
        ExecutionRequestEntry requests = new(execution);

        return new(execution, restorer.CreateRestored(context, data, requests));
    }

    /// <summary>运行一次执行并把结果收口。</summary>
    public async Task ExecuteAsync()
    {
        try
        {
            Execution.AttachJournal(runner.Journal);
            if (runner is IExecutionUsageSource usageReader)
            {
                Execution.AttachUsageReader(usageReader);
            }

            if (runner is IExecutionRestoreDataSource restoreDataReader)
            {
                Execution.AttachRestoreDataReader(restoreDataReader);
            }

            Execution.MarkRunning();
            ErrorOr<ExecutionOutput> outcome = await runner
                .ExecuteAsync(Execution.CancellationToken)
                .ConfigureAwait(false);
            if (outcome.IsError)
            {
                Execution.MarkFailed(outcome.ErrorsOrEmptyList);
            }
            else
            {
                Execution.MarkSucceeded(outcome.Value);
            }
        }
        catch (OperationCanceledException) when (Execution.CancellationToken.IsCancellationRequested)
        {
            Execution.MarkInterrupted();
        }
        catch (OperationCanceledException)
        {
            Execution.MarkFailed([Error.Failure("Execution.Timeout", "模型请求超时，执行未收口。")]);
        }
        catch (Exception ex)
        {
            Execution.MarkFailed([Error.Failure("Execution.Failure", $"执行失败：{ex.Message}")]);
        }
        finally
        {
            if (runner is IExecutionRestoreDataSource restoreDataReader)
            {
                Execution.SettleRestoreData(restoreDataReader);
            }

            if (runner is IExecutionUsageSource usageReader)
            {
                Execution.SettleUsage(usageReader.Usage);
            }
        }
    }
}