using ErrorOr;
using Kuroe.Kernel.Input.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Journal;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Ability.Runner.Input;

/// <summary>输入运行器：登记等待回答，收到回答后把回答作为执行内容交回。</summary>
internal sealed class InputExecutionRunner(
    IInputChannel channel,
    InputExecutionParameters parameters,
    IExecutionRequestEntry requests,
    TimeProvider time) : IExecutionRunner
{
    private readonly ExecutionJournal _journal = new(time);
    private readonly IExecutionRequestEntry _requests = requests;

    /// <inheritdoc/>
    public IProcessJournal Journal => _journal;

    /// <inheritdoc/>
    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        ExecutionRequest request = _requests.Request;
        TaskId task = new(request.Owner.Value);
        FlowStepPath step = parameters.Step;

        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.Accept(task, step, FlowPendingKind.Input, parameters.Question, text => received.TrySetResult(text));

        try
        {
            string answer = await received.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            _journal.ReportProgress("已收到回答。");

            return ExecutionOutput.FromContent(answer);
        }
        catch (Exception)
        {
            channel.Decline(task, step);
            throw;
        }
    }
}