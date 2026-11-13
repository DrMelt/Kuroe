using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程的提交与恢复。</summary>
public sealed class DialogueFlowSubmission(
    ModelService models,
    SettingsProvider settings,
    KuroePaths paths,
    IFlowExecutionGateway executions) : IFlowSubmission
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
        DialogueFlowState state = new(inputs, paths.WorkingDirectory, model.Value, language);

        return new FlowSubmission(
            state,
            string.IsNullOrWhiteSpace(title) ? DialogueFlow.Name.Value : title,
            state.StartTexts,
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, DialogueFlowAccess.Values(language)));
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

        string language = DialogueFlowAccess.Language(saved.Values, settings.AssistantLanguage);
        string model = DialogueFlowAccess.Model(saved.Values, models.Current?.Value ?? string.Empty);
        string workspace = DialogueFlowAccess.Workspace(saved.Values, paths.WorkingDirectory);
        DialogueFlowState state = new(saved.Inputs, workspace, model, language);
        List<IExecution> restored = [.. record.Executions.Select(execution => RestoreExecution(record.Identity.Id, execution))];

        return new FlowRestore(
            state,
            new FlowTexts(saved.StartTexts.Texts),
            restored,
            DialogueFlowState.AwaitingSeed);
    }

    private IExecution RestoreExecution(TaskId task, ExecutionRecord record)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId(task.Value),
            record.Target,
            LlmNodeTypes.Kind,
            record.Attempt,
            [],
            [.. record.Inputs.Select(input => new ExecutionInputValue(input.Port, input.Text, null))],
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