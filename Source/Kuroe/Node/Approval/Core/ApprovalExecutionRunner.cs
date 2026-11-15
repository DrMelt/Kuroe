using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Node.Approval;

/// <summary>审批节点的执行运行器：登记等待批准，收到批准后按停驻输入产出由节点输出投影发布。</summary>
internal sealed class ApprovalExecutionRunner(IInputChannel channel, string? question)
    : IExecutionRunner
{
    private readonly ExecutionJournal _journal = new();

    /// <inheritdoc/>
    public IProcessJournal Journal => _journal;

    /// <inheritdoc/>
    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        TaskId task = new(request.Owner.Value);
        FlowStepPath step = ExecutionTargetPaths.StepPath(request.Target);

        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.Accept(task, step, FlowPendingKind.Approve, question, text => received.TrySetResult(text));

        try
        {
            await received.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            _journal.ReportProgress("已批准。");

            return ExecutionOutput.FromPorts([]);
        }
        catch (OperationCanceledException)
        {
            channel.Decline(task, step);
            throw;
        }
        catch (Exception)
        {
            channel.Decline(task, step);
            throw;
        }
    }
}