using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue;

/// <summary>代码定义对话流程：提交、控制、快照与存档都按对话状态实现，不使用节点图。</summary>
public sealed class DialogueFlowFramework(
    ExecutionRuntime executions,
    IFlowExecutionGateway gateway,
    ModelService models,
    SettingsProvider settings,
    KuroePaths paths) : IFlowFramework
{
    private const string WorkspaceKey = "workspace";
    private const string ModelKey = "model";
    private const string LanguageKey = "language";

    /// <inheritdoc/>
    public string Kind => FlowRecordKinds.Code;

    /// <inheritdoc/>
    public string Name => "代码流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => name == DialogueFlow.Name;

    /// <inheritdoc/>
    public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title)
    {
        if (!Handles(name))
        {
            return [Error.Validation("Flow.Name", $"代码流程不处理流程 {name}。")];
        }

        if (models.Current is not { } model)
        {
            return ModelErrors.ModelNotSelected();
        }

        string language = settings.AssistantLanguage;
        DialogueFlowState state = new(inputs, paths.WorkingDirectory, model.Value, language);

        return new FlowSubmission(
            state,
            string.IsNullOrWhiteSpace(title) ? DialogueFlow.Name.Value : title,
            state.StartTexts,
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, Values(language)));
    }

    /// <inheritdoc/>
    public void Activate(WorkTask task)
    {
    }

    /// <inheritdoc/>
    public IFlowControl CreateControl(WorkTask task) =>
        new DialogueFlowControl(task, executions, gateway, State(task));

    /// <inheritdoc/>
    public IFlowView CaptureView(WorkTask task) =>
        DialogueView.Build(State(task), task.Control, task);

    /// <inheritdoc/>
    public TaskSnapshot CreateSnapshot(WorkTask task)
    {
        DialogueFlowState state = State(task);
        IFlowControl? control = task.Control;

        return new TaskSnapshot(
            task.Id,
            task.Title,
            task.Inputs,
            new PortTexts(state.StartTexts.Texts),
            DialogueView.Build(state, control, task, task.State),
            task.State,
            task.Executions.Count(execution => execution.IsLive),
            [],
            [],
            [],
            task.JournalEntries,
            task.DroppedJournal,
            task.CreatedAt,
            task.LastActivityAt)
        {
            Executions = [.. task.Executions.Select(execution => execution.Snapshot())],
            Pending = control?.Pending() ?? [],
        };
    }

    /// <inheritdoc/>
    public FlowRecord CaptureRecord(WorkTask task)
    {
        DialogueFlowState state = State(task);

        return new FlowRecord(
            FlowRecordKinds.Code,
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, Values(state.Language)),
            new CodeFlowStateRecord(state.Inputs, state.StartTexts, Values(state)));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ExecutionRecord> CaptureExecutions(WorkTask task) =>
        [.. task.Executions.Select(execution => execution.Snapshot().CaptureRecord())];

    private static DialogueFlowState State(WorkTask task) =>
        task.FlowState as DialogueFlowState
        ?? throw new InvalidOperationException("任务不是对话流程。");

    /// <inheritdoc/>
    public ErrorOr<FlowRestore> Restore(TaskRecord record)
    {
        if (record.Flow.Definition is not CodeFlowDefinitionRecord definition
            || definition.Implementation != DialogueFlow.Implementation)
        {
            return [Error.Failure("Flow.Restore", "存档不是对话流程定义。")];
        }

        if (record.Flow.State is not CodeFlowStateRecord saved)
        {
            return [Error.Failure("Flow.Restore", "存档缺少对话流程状态。")];
        }

        string language = saved.Values.GetValueOrDefault(LanguageKey) ?? settings.AssistantLanguage;
        string model = saved.Values.GetValueOrDefault(ModelKey) ?? models.Current?.Value ?? string.Empty;
        string workspace = saved.Values.GetValueOrDefault(WorkspaceKey) ?? paths.WorkingDirectory;
        DialogueFlowState state = new(saved.Inputs, workspace, model, language);
        List<Execution> restored = [.. record.Executions.Select(execution => RestoreExecution(record.Identity.Id, execution))];

        return new FlowRestore(
            state,
            new FlowTexts(saved.StartTexts.Texts),
            restored,
            DialogueFlowState.AwaitingSeed);
    }

    private static Execution RestoreExecution(TaskId task, ExecutionRecord record)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId(task.Value),
            record.Target,
            LlmNodeTypes.Name.Value,
            record.Attempt,
            [],
            [.. record.Inputs.Select(input => new ExecutionInputValue(input.Port, input.Text, null))],
            record.Texts,
            []);

        return Execution.Restore(
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
            record.ContinuedFrom);
    }

    private static IReadOnlyDictionary<string, string> Values(string language) =>
        new Dictionary<string, string> { [LanguageKey] = language };

    private static IReadOnlyDictionary<string, string> Values(DialogueFlowState state) =>
        new Dictionary<string, string>
        {
            [WorkspaceKey] = state.Workspace,
            [ModelKey] = state.Model,
            [LanguageKey] = state.Language,
        };
}