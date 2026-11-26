using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Maf.Control;

/// <summary>MAF 步骤运行时看到的流程控制：是否停止推进与中断上报。</summary>
internal interface IMafStepRunControl
{
    /// <summary>流程是否已停止推进。</summary>
    bool Halted { get; }

    /// <summary>上报步骤被中断。</summary>
    void Interrupt(string reason);
}

/// <summary>MAF 步骤的执行桥接：一次步骤运行对应一次 Kuroe 执行，产出作为消息交给下游。</summary>
internal sealed class MafStepExecutor(
    ITaskFlowContext task,
    ITaskExecutionGateway gateway,
    MafStep step,
    string model,
    IMafStepRunControl control)
{
    /// <summary>执行一次步骤，把交回的端口作为上游产出交给下游。</summary>
    internal async ValueTask<MafStepMessage> HandleAsync(
        IReadOnlyList<MafStepInput> inputs,
        CancellationToken cancellationToken)
    {
        if (control.Halted)
        {
            throw new InvalidOperationException($"步骤 {step.Id} 在流程停止后不再执行。");
        }

        ExecutionStepId stepId = step.ExecutionStepId;
        int attempt = task.Executions.Count(execution => execution.Request.Step == stepId) + 1;
        ExecutionRequest request = Request(inputs);
        ExecutionParameterContext context = new(
            request.Owner,
            request.Step,
            attempt,
            1,
            request.InputPorts,
            request.Inputs,
            request.Texts,
            request.OutputPorts,
            model);
        ErrorOr<ExecutionInvocation> invocation = step.InvocationFactory.Create(step.Id, model, context);
        if (invocation.IsError)
        {
            throw new InvalidOperationException(invocation.FirstError.Description);
        }

        IExecution execution = gateway.Open(task, request);
        lock (task.Gate)
        {
            task.AttachExecution(execution);
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(
            () => gateway.Interrupt(execution, "MAF 流程取消。"));

        ErrorOr<Success> run = await gateway.RunAsync(execution, invocation.Value).ConfigureAwait(false);

        ExecutionSnapshot snapshot = execution.Snapshot();
        if (snapshot.State == ExecutionState.Interrupted)
        {
            string reason = snapshot.Failures.Count > 0 ? snapshot.Failures[0] : "执行被中断。";
            control.Interrupt(reason);

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

    /// <summary>本次执行的请求：输入按端口聚合，步骤输出端口要求交回。</summary>
    private ExecutionRequest Request(IReadOnlyList<MafStepInput> inputs)
    {
        ExecutionOwnerId owner = new(task.Id.Value);
        IReadOnlyList<ExecutionInputPort> ports =
        [
            .. inputs
                .GroupBy(input => input.Port)
                .Select(group =>
                {
                    MafStepInput first = group.First();

                    return new ExecutionInputPort(
                        new ExecutionPortName(first.Port.Value),
                        first.Kind,
                        first.Role,
                        group.Any(input => input.Required),
                        first.DefaultText);
                }),
        ];
        IReadOnlyList<ExecutionInputValue> values =
        [
            .. inputs
                .Where(input => input.Role == ExecutionPortRole.Content)
                .Select(input => new ExecutionInputValue(
                    new ExecutionPortName(input.Port.Value),
                    input.Text,
                    SourceText(input),
                    RecordKey(input))),
        ];
        IReadOnlyDictionary<ExecutionPortName, string> texts = inputs
            .Where(input => input.Role != ExecutionPortRole.Control)
            .GroupBy(input => input.Port)
            .ToDictionary(
                group => new ExecutionPortName(group.Key.Value),
                group => string.Join("\n\n", group.Select(input => input.Text).Where(text => text.Length > 0)));

        return new ExecutionRequest(
            owner,
            step.ExecutionStepId,
            ports,
            values,
            texts,
            [.. step.OutputPorts().Select(port => new ExecutionOutputPort(
                new ExecutionPortName(port.Name.Value),
                port.Kind,
                port.Required,
                port.Trigger))]);
    }

    /// <summary>输入取用的来源描述，未给出时按上游端口给出。</summary>
    private static string? SourceText(MafStepInput input) =>
        input.Source ?? (input.UpstreamStep is { } upstream
            ? MafRecordSources.Display(new MafStepOutput(upstream, input.UpstreamPort ?? input.Port, input.Text))
            : null);

    /// <summary>输入取用的记录标识，未给出时按上游端口给出。</summary>
    private static string? RecordKey(MafStepInput input) =>
        input.SourceKey ?? (input.UpstreamStep is { } upstream
            ? MafRecordSources.Write(new MafStepOutput(upstream, input.UpstreamPort ?? input.Port, input.Text))
            : null);

    /// <summary>本次执行交回的端口作为下游消息。</summary>
    private MafStepMessage Output(ExecutionSnapshot snapshot) =>
        new(
            step.Id,
            [.. (snapshot.Contract.Outputs ?? new Dictionary<ExecutionPortName, ExecutionOutputValue>())
                .Select(pair => new MafStepOutput(
                    step.Id,
                    new PortName(pair.Key.Value),
                    pair.Value.Text ?? string.Empty)
                {
                    Fires = pair.Value.Fires,
                })]);
}