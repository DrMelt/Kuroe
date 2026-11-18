using System.Text.Json;
using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Execution.Records.Shared;
using Kuroe.Maf.Records.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Execution.Shared.State;
using Kuroe.Execution.Shared.Usage;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的提交与恢复：校验开始输入，建立流程状态，从存档重建执行。</summary>
public sealed class MafFlowSubmission(
    IMafFlowSource flows,
    ITaskExecutionGateway executions) : IFlowSubmission
{
    /// <inheritdoc/>
    public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title)
    {
        ErrorOr<MafFlowDefinition> found = flows.Find(name);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        MafFlowDefinition definition = found.Value;
        if (definition.Validate() is { Count: > 0 } invalid)
        {
            return [.. invalid];
        }

        ErrorOr<MafStep> entry = definition.Entry();
        if (entry.IsError)
        {
            return entry.ErrorsOrEmptyList;
        }

        if (CheckInputs(definition, inputs) is { Count: > 0 } errors)
        {
            return errors;
        }

        StartInputs effective = FillDefaults(definition, inputs);
        if (effective.Text(MafFlowDefinition.ModelPort) is not { Length: > 0 } model)
        {
            return [Error.Validation(FlowErrorCodes.TaskInput,
                $"开始端口 {MafFlowDefinition.ModelPort} 没有给出模型。")];
        }

        ErrorOr<Success> invocations = ValidateInvocations(definition, model, id);
        if (invocations.IsError)
        {
            return invocations.ErrorsOrEmptyList;
        }

        MafFlowState state = new(definition, effective, new FlowTexts(Texts(effective)), model);

        return new FlowSubmission(
            state,
            TaskTitle.Compose(effective, name, title),
            state.StartTexts,
            new MafFlowDefinitionRecord(MafFlowIdentity.Implementation, name.Value));
    }

    /// <inheritdoc/>
    public ErrorOr<FlowRestore> Restore(TaskRecord record)
    {
        if (record.Flow.Definition is not MafFlowDefinitionRecord definition
            || definition.Implementation != MafFlowIdentity.Implementation)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, "存档不是 MAF 流程定义。")];
        }

        if (record.Flow.State is not MafFlowStateRecord saved)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, "存档缺少 MAF 流程状态。")];
        }

        ErrorOr<MafFlowDefinition> found = flows.Find(record.Flow.Identity.Name);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        MafFlowDefinition flow = found.Value;
        if (flow.Validate() is { Count: > 0 } invalid)
        {
            return [.. invalid];
        }

        ErrorOr<MafStep> entry = flow.Entry();
        if (entry.IsError)
        {
            return entry.ErrorsOrEmptyList;
        }

        string model = saved.Model ?? string.Empty;
        if (model.Length == 0)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, "MAF 流程状态缺少模型。")];
        }
        MafWorkflowCheckpoint? checkpoint = (saved.CheckpointSession, saved.CheckpointId, saved.CheckpointData) switch
        {
            ({ } session, { } id, { } data) => new MafWorkflowCheckpoint(session, id, data),
            _ => null,
        };
        if (checkpoint is { } savedCheckpoint)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(savedCheckpoint.Data);
            }
            catch (JsonException ex)
            {
                return [Error.Failure(
                    FlowErrorCodes.RecordFormat,
                    $"MAF 检查点不合法：{ex.Message}")];
            }
        }

        ErrorOr<Success> invocations = ValidateInvocations(flow, model, record.Identity.Id);
        if (invocations.IsError)
        {
            return invocations.ErrorsOrEmptyList;
        }

        MafFlowState state = new(
            flow,
            saved.Inputs,
            new FlowTexts(saved.StartTexts.Texts),
            model)
        {
            Checkpoint = checkpoint,
        };

        List<IExecution> restored = [];
        foreach (ExecutionRecord execution in record.Executions)
        {
            Error? failure = TryRestoreExecution(
                record.Identity.Id,
                flow,
                execution,
                out IExecution? restoredExecution);
            if (failure is { } error)
            {
                return [error];
            }

            if (restoredExecution is null)
            {
                return [Error.Failure(FlowErrorCodes.RecordFormat, "恢复执行没有建立。")];
            }

            restored.Add(restoredExecution);
        }

        bool interrupted = restored.Any(execution =>
            execution.Snapshot().State is ExecutionState.Queued or ExecutionState.Running or ExecutionState.Interrupted);
        if (interrupted)
        {
            state.Checkpoint = null;
        }

        IReadOnlyList<MafStepId> pending = interrupted
            ? [.. flow.Steps.Select(step => step.Id)]
            : [.. saved.PendingSteps
                .Select(value => MafStepId.Create(value))
                .Where(result => result.IsSuccess)
                .Select(result => result.Value)];

        return new FlowRestore(
            state,
            state.StartTexts,
            restored,
            new FlowSeed([.. pending.Select(step => step.ToPath())]));
    }

    /// <summary>按存档重建一次已收口的执行。</summary>
    private Error? TryRestoreExecution(
        TaskId taskId,
        MafFlowDefinition definition,
        ExecutionRecord record,
        out IExecution? execution)
    {
        execution = null;
        MafStep? step = definition.Steps.FirstOrDefault(candidate => candidate.ExecutionStepId == record.Step);
        if (step is null)
        {
            return Error.Failure(FlowErrorCodes.RecordFormat,
                $"存档里的步骤 {record.Step.Value} 不在流程定义中。");
        }

        ExecutionRequest request = new(
            new ExecutionOwnerId(taskId.Value),
            record.Step,
            [.. record.Inputs.Select(input => new ExecutionInputPort(
                input.Port,
                ExecutionPortKind.Text,
                ExecutionPortRole.Content,
                Required: true,
                DefaultText: null))],
            [.. record.Inputs.Select(input => new ExecutionInputValue(
                input.Port,
                input.Text,
                MafRecordSources.Display(input.Source),
                input.Source))],
            record.Texts,
            [.. step.Outputs.Select(port => new ExecutionOutputPort(
                new ExecutionPortName(port.Value),
                ExecutionPortKind.Text,
                Required: true,
                CarriesTrigger: false))]);

        IExecution restored = executions.Restore(
            new ExecutionRestorePlan(
                record.Id,
                request,
                record.CreatedAt,
                record.State,
                record.Progress,
                record.StartedAt,
                record.FinishedAt,
                record.Failures,
                new ExecutionUsage(
                    record.Usage.RequestCount,
                    record.Usage.RequestsWithoutUsage,
                    new TokenUsage(
                        record.Usage.Tokens.Input,
                        record.Usage.Tokens.Output,
                        record.Usage.Tokens.Total,
                        record.Usage.Tokens.CachedInput,
                        record.Usage.Tokens.Reasoning))
                {
                    ContextTokens = record.Usage.ContextTokens,
                },
                record.RestoreData is { } restore ? new ExecutionRestoreState(restore.Format, restore.Payload) : null,
                record.Contract is { } contract
                    ? new ExecutionContract(contract.Outputs, contract.CannotCompleteReason)
                    : ExecutionContract.Empty,
                record.ContinuedFrom));

        execution = restored;

        return null;
    }

    /// <summary>按模型校验全部步骤的执行装配；装配建立失败或步骤标识不一致时返回错误。</summary>
    private static ErrorOr<Success> ValidateInvocations(
        MafFlowDefinition definition,
        string model,
        TaskId task)
    {
        List<Error> errors = [];
        foreach (MafStep step in definition.Steps)
        {
            ExecutionParameterContext context = new(
                new ExecutionOwnerId(task.Value),
                step.ExecutionStepId,
                1,
                1,
                [],
                [],
                new Dictionary<ExecutionPortName, string>(),
                [],
                model);
            ErrorOr<ExecutionInvocation> invocation = step.InvocationFactory.Create(step.Id, model, context);
            if (invocation.IsError)
            {
                errors.AddRange(invocation.ErrorsOrEmptyList);
                continue;
            }

            if (invocation.Value.Step != step.ExecutionStepId)
            {
                errors.Add(FlowErrors.Body(
                    definition.Name.Value,
                    $"步骤「{step.Id.Value}」的执行步骤标识 {invocation.Value.Step.Value} 与步骤标识不一致。"));
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>提交输入必须覆盖全部不带默认值的开始端口，不能重复，也不能落在未声明的端口上。</summary>
    private static List<Error> CheckInputs(MafFlowDefinition definition, StartInputs inputs) =>
    [
        .. inputs.Values
            .GroupBy(input => input.Port)
            .Where(group => group.Count() > 1)
            .Select(group => DuplicateInput(group.Key)),
        .. inputs.Values
            .Where(input => definition.StartPorts.All(port => port.Name != input.Port))
            .Select(input => UnknownInput(input.Port)),
        .. definition.StartPorts
            .Where(port => port.Default is null && inputs.Text(port.Name) is null)
            .Select(port => MissingInput(port.Name)),
    ];

    /// <summary>把省略的默认值端口补进开始输入。</summary>
    private static StartInputs FillDefaults(MafFlowDefinition definition, StartInputs inputs)
    {
        List<StartInput> values = [.. inputs.Values];
        foreach (MafStartPort port in definition.StartPorts)
        {
            if (port.Default is { Length: > 0 } value && inputs.Text(port.Name) is null)
            {
                values.Add(new StartInput(port.Name, value));
            }
        }

        return new StartInputs(values);
    }

    private static Dictionary<PortName, string> Texts(StartInputs inputs) =>
        inputs.Values.ToDictionary(input => input.Port, input => input.Text);

    private static Error MissingInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 没有输入。");

    private static Error UnknownInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"流程没有开始端口 {port}。");

    private static Error DuplicateInput(PortName port) =>
        Error.Validation(FlowErrorCodes.TaskInput, $"开始端口 {port} 的输入重复。");
}