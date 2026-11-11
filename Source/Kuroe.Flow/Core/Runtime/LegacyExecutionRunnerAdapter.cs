using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using NeutralExecutionOutput = Kuroe.Flow.Execution.ExecutionOutput;
using NeutralExecutionOutputKind = Kuroe.Flow.Execution.ExecutionOutputKind;
using OldExecutionRunner = Kuroe.Flow.Shared.RuntimeGraph.Execution.IExecutionRunner;
using OldExecutionOutput = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionOutput;
using OldExecutionOutputKind = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionOutputKind;

namespace Kuroe.Flow.Runtime;

/// <summary>图侧旧运行器到中性运行器的适配入口。</summary>
public static class ExecutionRunnerAdapter
{
    /// <summary>把图侧旧运行器包装为中性运行器，执行请求取给出的图侧输入。</summary>
    public static Kuroe.Flow.Execution.IExecutionRunner Adapt(
        ExecutionInput input,
        Kuroe.Flow.Shared.RuntimeGraph.Execution.IExecutionRunner runner) =>
        new LegacyExecutionRunnerAdapter(input, runner);
}

/// <summary>图侧旧运行器的中性适配：执行请求取图侧输入，过程记录、用量、恢复数据与产出按中性形状转出。</summary>
internal sealed class LegacyExecutionRunnerAdapter(ExecutionInput input, OldExecutionRunner runner)
    : Kuroe.Flow.Execution.IExecutionRunner, IExecutionUsageSource, IExecutionRestoreDataSource
{
    /// <inheritdoc/>
    public IProcessJournal Journal => runner.Journal;

    /// <inheritdoc/>
    public ExecutionUsageSnapshot Usage => runner is IExecutionUsageReader reader
        ? Snapshot(reader.Usage)
        : ExecutionUsageSnapshot.None;

    /// <inheritdoc/>
    public ExecutionRestoreState? RestoreData => runner is IExecutionRestoreDataReader { RestoreData: { } data }
        ? new ExecutionRestoreState(data.Format, data.Payload)
        : null;

    /// <inheritdoc/>
    public event Action? RestoreDataChanged
    {
        add
        {
            if (runner is IExecutionRestoreDataReader reader)
            {
                reader.RestoreDataChanged += value;
            }
        }
        remove
        {
            if (runner is IExecutionRestoreDataReader reader)
            {
                reader.RestoreDataChanged -= value;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ErrorOr<NeutralExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ErrorOr<OldExecutionOutput> outcome = await runner.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.ErrorsOrEmptyList;
        }

        return outcome.Value.Kind == OldExecutionOutputKind.Content
            ? NeutralExecutionOutput.FromContent(outcome.Value.Content)
            : NeutralExecutionOutput.FromPorts([]);
    }

    private static ExecutionUsageSnapshot Snapshot(ExecutionUsage usage) =>
        new(
            usage.RequestCount,
            usage.RequestsWithoutUsage,
            new TokenUsageSnapshot(
                usage.Tokens.Input,
                usage.Tokens.Output,
                usage.Tokens.Total,
                usage.Tokens.CachedInput,
                usage.Tokens.Reasoning),
            usage.ContextTokens);
}