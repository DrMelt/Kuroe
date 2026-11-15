using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 步骤的执行桥接：一次步骤运行对应一次 Kuroe 执行，产出作为消息交给下游。</summary>
internal sealed class MafStepExecutor(
    ITaskFlowContext task,
    ITaskExecutionGateway gateway,
    MafStep step,
    string model,
    Func<bool> halted,
    Action<string> interrupted)
{
    /// <summary>执行一次步骤，把交回的端口作为上游产出交给下游。</summary>
    internal async ValueTask<MafStepMessage> HandleAsync(
        MafStepMessage message,
        CancellationToken cancellationToken)
    {
        if (halted())
        {
            throw new InvalidOperationException($"步骤 {step.Id} 在流程停止后不再执行。");
        }

        IExecutionTarget target = step.Target(model);
        ExecutionTargetId targetId = new(step.Id);
        if (target.Id != targetId)
        {
            throw new InvalidOperationException(
                $"步骤 {step.Id} 的执行目标标识 {target.Id.Value} 与步骤标识不一致。");
        }

        int attempt = task.Executions.Count(execution => execution.Request.Target == targetId) + 1;
        IExecution execution = gateway.Open(task, Request(message, target, attempt));
        lock (task.Gate)
        {
            task.AttachExecution(execution);
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => gateway.Interrupt(execution, "MAF 流程取消。"));

        ErrorOr<Success> run = await gateway.RunAsync(execution, null, target).ConfigureAwait(false);

        ExecutionSnapshot snapshot = execution.Snapshot();
        if (snapshot.State == ExecutionState.Interrupted)
        {
            string reason = snapshot.Failures.Count > 0 ? snapshot.Failures[0] : "执行被中断。";
            interrupted(reason);

            throw new InvalidOperationException($"步骤 {step.Id} 被中断：{reason}");
        }

        if (snapshot.State != ExecutionState.Succeeded)
        {
            string reason = "执行未收口。";
            if (snapshot.Failures.Count > 0)
            {
                reason = snapshot.Failures[0];
            }
            else if (run.IsError)
            {
                reason = run.FirstError.Description;
            }

            throw new InvalidOperationException($"步骤 {step.Id} 失败：{reason}");
        }

        return Output(snapshot);
    }

    /// <summary>本次执行的请求：上游文本作为输入，步骤输出端口要求交回。</summary>
    private ExecutionRequest Request(MafStepMessage message, IExecutionTarget target, int attempt) =>
        new(
            new ExecutionOwnerId(task.Id.Value),
            target.Id,
            target.Kind,
            attempt,
            [.. message.Outputs.Select(output => new ExecutionInputPort(
                new ExecutionPortName(output.Port),
                ExecutionPortKind.Text,
                ExecutionPortRole.Content,
                Required: true,
                DefaultText: null))],
            [.. message.Outputs.Select(output => new ExecutionInputValue(
                new ExecutionPortName(output.Port),
                output.Text,
                MafRecordSources.Display(output),
                MafRecordSources.Write(output)))],
            new Dictionary<ExecutionPortName, string>(),
            [.. step.Outputs.Select(port => new ExecutionOutputPort(
                new ExecutionPortName(port.Value),
                ExecutionPortKind.Text,
                Required: true,
                CarriesTrigger: false))]);

    /// <summary>本次执行交回的端口作为下游消息。</summary>
    private MafStepMessage Output(ExecutionSnapshot snapshot) =>
        new(
            step.Id,
            [.. (snapshot.Contract.Outputs ?? new Dictionary<ExecutionPortName, ExecutionOutputValue>())
                .Select(pair => new MafStepOutput(step.Id, pair.Key.Value, pair.Value.Text ?? string.Empty))]);
}