using ErrorOr;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Journal;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Approval;

/// <summary>审批运行器：登记等待批准，收到批准后按停驻输入产出由节点输出投影发布。</summary>
internal sealed class ApprovalExecutionRunner(IInputChannel channel, ApprovalExecutionParameters parameters)
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
        FlowStepPath step = parameters.Step;

        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.Accept(task, step, FlowPendingKind.Approve, parameters.Question, text => received.TrySetResult(text));

        try
        {
            await received.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            _journal.ReportProgress("已批准。");

            return ExecutionOutput.FromPorts([]);
        }
        catch (Exception)
        {
            channel.Decline(task, step);
            throw;
        }
    }
}