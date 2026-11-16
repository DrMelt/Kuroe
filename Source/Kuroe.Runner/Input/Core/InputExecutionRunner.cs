using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Runner.Input;

/// <summary>输入运行器：登记等待回答，收到回答后把回答作为执行内容交回。</summary>
internal sealed class InputExecutionRunner(IInputChannel channel, InputExecutionParameters parameters)
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