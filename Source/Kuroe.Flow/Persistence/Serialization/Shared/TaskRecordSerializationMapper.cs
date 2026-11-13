using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;
using RecordExecutionId = Kuroe.Flow.Execution.ExecutionId;
using RecordExecutionPortName = Kuroe.Flow.Execution.ExecutionPortName;
using RecordExecutionOutputValue = Kuroe.Flow.Execution.ExecutionOutputValue;

namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>任务存档与通用存档文档之间的映射与校验。流程定义与状态交给按身份注册的编解码器。</summary>
public sealed class TaskRecordSerializationMapper(IReadOnlyList<IFlowRecordCodec> codecs)
{
    private readonly IReadOnlyList<IFlowRecordCodec> _codecs = codecs;

    /// <summary>把任务存档写成通用文档，写入当前版本。</summary>
    public ErrorOr<TaskRecordDocument> ToDocument(TaskRecord record)
    {
        try
        {
            return MapDocument(record);
        }
        catch (NotSupportedException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordWrite, $"写出存档失败：{ex.Message}")];
        }
    }

    /// <summary>把通用文档还原为任务存档，版本不符或字段不合法时返回错误。</summary>
    public ErrorOr<TaskRecord> FromDocument(TaskRecordDocument? document)
    {
        if (document is null)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, "存档为空。")];
        }

        if (!TaskRecordSerializationVersion.IsReadable(document.Version))
        {
            return [Error.Failure(FlowErrorCodes.RecordVersion, $"不支持的存档版本 {document.Version}。")];
        }

        try
        {
            return ReadTask(document.Task);
        }
        catch (FormatException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, ex.Message)];
        }
        catch (ArgumentException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, ex.Message)];
        }
    }

    private TaskRecordDocument MapDocument(TaskRecord record) =>
        new(
            TaskRecordSerializationVersion.Current,
            new TaskRecordData(
                new TaskIdentityData(
                    record.Identity.Id.Value,
                    record.Identity.Title,
                    record.Identity.CreatedAt,
                    record.Identity.LastActivityAt),
                record.SavedAt,
                record.Lifecycle,
                record.Completed,
                new TaskJournalData([.. record.Journal.Entries.Select(ToData)], record.Journal.DroppedEntries),
                ToData(record.Flow),
                [.. record.Executions.Select(ToData)]));

    private FlowData ToData(FlowRecord flow)
    {
        IFlowRecordCodec codec = Find(flow.Identity, writing: true)
            ?? throw new NotSupportedException($"没有流程记录编解码处理 {flow.Identity.Kind}/{flow.Identity.Implementation}。");
        FlowRecordData data = codec.Write(flow.Definition, flow.State);

        return new FlowData(
            flow.Identity.Name.Value,
            flow.Identity.Kind,
            flow.Identity.Implementation,
            data.Definition,
            data.State);
    }

    private static JournalEntryData ToData(JournalEntry entry) =>
        new(entry.At, entry.Level, entry.Label, entry.Text);

    private static ExecutionData ToData(ExecutionRecord execution) =>
        new(
            execution.Id.Value,
            execution.Target.Value,
            execution.Attempt,
            execution.State,
            execution.Progress,
            execution.CreatedAt,
            execution.StartedAt,
            execution.FinishedAt,
            [.. execution.Failures],
            ToData(execution.Usage),
            [.. execution.Inputs.Select(ToData)],
            execution.Texts.ToDictionary(pair => pair.Key.Value, pair => pair.Value),
            ToData(execution.RestoreData),
            execution.ContinuedFrom?.Value,
            ToData(execution.Contract));

    private static ExecutionInputData ToData(ExecutionInputRecord input) =>
        new(input.Port.Value, input.Source, input.Text);

    private static RestorePayloadData? ToData(ExecutionRestoreRecord? data) =>
        data is null
            ? null
            : new RestorePayloadData(data.Format, data.Payload);

    private static ExecutionContractData? ToData(ExecutionContractRecord? contract)
    {
        if (contract is null)
        {
            return null;
        }

        List<PortValueData>? outputs = null;
        if (contract.Outputs is { } values)
        {
            outputs =
            [
                .. values.Select(pair => new PortValueData(
                    pair.Key.Value,
                    pair.Value.Text,
                    pair.Value.Fires)),
            ];
        }

        return new ExecutionContractData(outputs, contract.CannotCompleteReason);
    }

    private static ExecutionUsageData ToData(ExecutionUsageRecord usage) =>
        new(usage.RequestCount, usage.RequestsWithoutUsage, ToData(usage.Tokens), usage.ContextTokens);

    private static TokenUsageData ToData(TokenUsageRecord tokens) =>
        new(tokens.Input, tokens.Output, tokens.Total, tokens.CachedInput, tokens.Reasoning);
private TaskRecord ReadTask(TaskRecordData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("存档缺少任务。");
        }

        TaskIdentityData identity = dto.Identity ?? throw new FormatException("存档缺少任务身份。");
        FlowData flow = dto.Flow ?? throw new FormatException("存档缺少流程记录。");
        TaskJournalData journal = dto.Journal ?? throw new FormatException("存档缺少任务过程记录。");
        DateTimeOffset savedAt = dto.SavedAt ?? identity.LastActivityAt;

        return new TaskRecord(
            new TaskIdentity(
                ReadId(identity.Id),
                RequiredText(identity.Title, "任务标题"),
                identity.CreatedAt,
                identity.LastActivityAt),
            savedAt,
            dto.Lifecycle,
            dto.Completed,
            new TaskJournalRecord(ReadJournal(journal.Entries), journal.DroppedEntries),
            ReadFlow(flow),
            [.. (dto.Executions ?? []).Select(ReadExecution)]);
    }

    private FlowRecord ReadFlow(FlowData dto)
    {
        string kind = RequiredText(dto.Kind, "流程种类");
        FlowIdentity identity = new(
            new FlowName(RequiredText(dto.Name, "流程名")),
            kind,
            RequiredText(dto.Implementation, "实现标识"));
        IFlowRecordCodec codec = Find(identity, writing: false)
            ?? throw new FormatException($"没有流程记录编解码处理 {kind}/{identity.Implementation}。");
        FlowRecordData data = new(
            dto.Definition ?? throw new FormatException("存档缺少流程定义。"),
            dto.State ?? throw new FormatException("存档缺少流程状态。"));

        return new FlowRecord(identity, codec.ReadDefinition(data), codec.ReadState(data));
    }

    private static ExecutionRecord ReadExecution(ExecutionData dto) =>
        new(
            new RecordExecutionId(dto.Id),
            new ExecutionTargetId(RequiredText(dto.Target, "执行目标")),
            dto.Attempt,
            dto.State,
            dto.Progress ?? string.Empty,
            dto.CreatedAt,
            dto.StartedAt,
            dto.FinishedAt,
            dto.Failures ?? [],
            ReadUsage(dto.Usage),
            [.. (dto.Inputs ?? []).Select(ReadExecutionInput)],
            ReadExecutionTexts(dto.Texts),
            ReadRestoreData(dto.RestoreData),
            dto.ContinuedFrom is { } continuedFrom ? new RecordExecutionId(continuedFrom) : null,
            ReadContract(dto.Contract));

    private static ExecutionInputRecord ReadExecutionInput(ExecutionInputData dto) =>
        new(
            new RecordExecutionPortName(RequiredText(dto.Port, "输入端口")),
            dto.Source ?? string.Empty,
            dto.Text ?? string.Empty);

    private static Dictionary<RecordExecutionPortName, string> ReadExecutionTexts(
        Dictionary<string, string>? values) =>
        values is null
            ? []
            : values.ToDictionary(pair => new RecordExecutionPortName(pair.Key), pair => pair.Value);

    private static ExecutionContractRecord? ReadContract(ExecutionContractData? dto)
    {
        if (dto is null)
        {
            return null;
        }

        Dictionary<RecordExecutionPortName, RecordExecutionOutputValue>? outputs = dto.Outputs?.ToDictionary(
            value => new RecordExecutionPortName(RequiredText(value.Port, "契约端口")),
            value => new RecordExecutionOutputValue(
                new RecordExecutionPortName(RequiredText(value.Port, "契约端口")),
                value.Text,
                value.Trigger ?? true));

        return new ExecutionContractRecord(outputs, dto.CannotCompleteReason);
    }

    private static ExecutionRestoreRecord? ReadRestoreData(RestorePayloadData? dto) =>
        dto is null
            ? null
            : new ExecutionRestoreRecord(
                dto.Format ?? throw new FormatException("恢复数据缺少格式。"),
                dto.Payload ?? throw new FormatException("恢复数据缺少负载。"));

    private static ExecutionUsageRecord ReadUsage(ExecutionUsageData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("执行记录缺少用量。");
        }

        if (dto.Tokens is null)
        {
            throw new FormatException("执行记录缺少 token 用量。");
        }

        return new ExecutionUsageRecord(
            dto.RequestCount,
            dto.RequestsWithoutUsage,
            ReadTokenUsage(dto.Tokens),
            dto.ContextTokens);
    }

    private static TokenUsageRecord ReadTokenUsage(TokenUsageData dto) =>
        new(dto.Input, dto.Output, dto.Total, dto.CachedInput, dto.Reasoning);

    private static IReadOnlyList<JournalEntry> ReadJournal(List<JournalEntryData>? entries) =>
        entries is null
            ? []
            : [.. entries.Select(entry => new JournalEntry(
                entry.At,
                entry.Level,
                RequiredText(entry.Label, "过程记录标签"),
                RequiredText(entry.Text, "过程记录文本")))];

    private IFlowRecordCodec? Find(FlowIdentity identity, bool writing)
    {
        IFlowRecordCodec[] found = [.. _codecs.Where(codec => codec.Handles(identity))];

        return found.Length switch
        {
            1 => found[0],
            0 => null,
            _ => writing
                ? throw new NotSupportedException($"多个流程记录编解码同时处理 {identity.Kind}/{identity.Implementation}。")
                : throw new FormatException($"多个流程记录编解码同时处理 {identity.Kind}/{identity.Implementation}。"),
        };
    }

    private static TaskId ReadId(string? text) =>
        TaskId.TryParse(text, out TaskId id)
            ? id
            : throw new FormatException($"任务键不合法：{text ?? "空"}。");

    private static string RequiredText(string? text, string field) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new FormatException($"存档缺少{field}。")
            : text;
}