using Kuroe.Flow.Execution.Shared;

namespace Kuroe.Flow.Execution.Records.Shared;

/// <summary>把中性执行快照捕获为存档记录。</summary>
public static class ExecutionRecordCapture
{
    /// <summary>捕获一次执行的状态、输入、用量、恢复数据与契约。</summary>
    public static ExecutionRecord CaptureRecord(this ExecutionSnapshot snapshot) =>
        new(
            snapshot.Id,
            snapshot.Request.Target,
            snapshot.Request.Attempt,
            snapshot.State,
            snapshot.Progress,
            snapshot.CreatedAt,
            snapshot.StartedAt,
            snapshot.FinishedAt,
            snapshot.Failures,
            new ExecutionUsageRecord(
                snapshot.Usage.RequestCount,
                snapshot.Usage.RequestsWithoutUsage,
                new TokenUsageRecord(
                    snapshot.Usage.Tokens.Input,
                    snapshot.Usage.Tokens.Output,
                    snapshot.Usage.Tokens.Total,
                    snapshot.Usage.Tokens.CachedInput,
                    snapshot.Usage.Tokens.Reasoning),
                snapshot.Usage.ContextTokens),
            [.. snapshot.Request.Inputs.Select(input => new ExecutionInputRecord(
                input.Port,
                input.SourceKey is { Length: > 0 } key ? key : input.Source ?? string.Empty,
                input.Text))],
            snapshot.Request.Texts,
            snapshot.RestoreData is { } restore
                ? new ExecutionRestoreRecord(restore.Format, restore.Payload)
                : null,
            snapshot.ContinuedFrom,
            snapshot.Contract is { Outputs: null, CannotCompleteReason: null }
                ? null
                : new ExecutionContractRecord(
                    snapshot.Contract.Outputs,
                    snapshot.Contract.CannotCompleteReason));
}