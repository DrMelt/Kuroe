using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Control.Shared;
using Kuroe.Execution.Records.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Node.Llm;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话流程的提交与恢复。</summary>
internal sealed class DialogueFlowSubmission(
    ModelService models,
    SettingsProvider settings,
    KuroePaths paths,
    ITaskExecutionGateway executions) : IFlowSubmission
{
    /// <inheritdoc/>
    public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title)
    {
        if (name != DialogueFlow.Name)
        {
            return [Error.Validation("Flow.Name", $"代码流程不处理流程 {name}。")];
        }

        if (models.Current is not { } model)
        {
            return ModelErrors.ModelNotSelected();
        }

        string language = settings.AssistantLanguage;
        DialogueMode mode = DialogueMode.Parse(inputs.Text(DialogueFlow.ModePort));
        DialogueFlowState state = new(inputs, paths.WorkingDirectory, model.Value, language, mode);

        return new FlowSubmission(
            state,
            string.IsNullOrWhiteSpace(title) ? DialogueFlow.Name.Value : title,
            state.StartTexts,
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, DialogueFlow.DefinitionValues(language)));
    }

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

        string language = saved.Values.GetValueOrDefault(DialogueFlow.LanguageKey) ?? settings.AssistantLanguage;
        string model = saved.Values.GetValueOrDefault(DialogueFlow.ModelKey) ?? models.Current?.Value ?? string.Empty;
        string workspace = saved.Values.GetValueOrDefault(DialogueFlow.WorkspaceKey) ?? paths.WorkingDirectory;
        DialogueFlowState state = DialogueFlowState.From(saved, language, model, workspace);
        List<IExecution> restored = [.. record.Executions.Select(execution => RestoreExecution(record.Identity.Id, execution))];

        return new FlowRestore(
            state,
            new FlowTexts(saved.StartTexts.Texts),
            restored,
            DialogueFlow.ReplySeed);
    }

    private IExecution RestoreExecution(TaskId task, ExecutionRecord record)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId(task.Value),
            record.Target,
            LlmNodeTypes.Kind,
            record.Attempt,
            [],
            [.. record.Inputs.Select(input => new ExecutionInputValue(
                input.Port,
                input.Text,
                input.Source,
                input.Source))],
            record.Texts,
            []);

        return executions.Restore(
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
    }
}