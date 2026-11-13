using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;
using ExecutionId = Kuroe.Flow.Execution.ExecutionId;
using RecordExecutionId = Kuroe.Flow.Execution.ExecutionId;
using RecordExecutionPortName = Kuroe.Flow.Execution.ExecutionPortName;
using RecordExecutionOutputValue = Kuroe.Flow.Execution.ExecutionOutputValue;

namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>任务存档与通用存档文档之间的映射与校验。</summary>
public static class TaskRecordSerializationMapper
{
    /// <summary>把任务存档写成通用文档，写入当前版本。</summary>
    public static ErrorOr<TaskRecordDocument> ToDocument(TaskRecord record)
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
    public static ErrorOr<TaskRecord> FromDocument(TaskRecordDocument? document)
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

    private static TaskRecordDocument MapDocument(TaskRecord record) =>
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

    private static FlowData ToData(FlowRecord flow) =>
        flow switch
        {
            { Identity.Kind: FlowRecordKinds.Graph, Definition: GraphFlowDefinitionRecord definition, State: GraphFlowStateRecord state } =>
                new FlowData(
                    flow.Identity.Name.Value,
                    flow.Identity.Kind,
                    flow.Identity.Implementation,
                    new GraphFlowDefinitionData(definition.Name.Value, definition.Nodes, definition.Flow),
                    new GraphFlowStateData(
                        new TaskStartData(
                            [.. state.Start.Inputs.Values.Select(input => new StartInputData(input.Port.Value, input.Text))],
                            ToData(state.Start.StartTexts)),
                        [.. state.Nodes.Select(ToData)],
                        [.. state.Ports.Select(ToData)],
                        new SchedulerSeedData([.. state.Pending.Steps.Select(step => ToData(NodePath.Of([.. step.Levels])))]))),
            { Identity.Kind: FlowRecordKinds.Code, Definition: CodeFlowDefinitionRecord definition, State: CodeFlowStateRecord state } =>
                new FlowData(
                    flow.Identity.Name.Value,
                    flow.Identity.Kind,
                    flow.Identity.Implementation,
                    null,
                    null,
                    new CodeFlowDefinitionData(definition.Implementation, ToData(definition.Values)),
                    new CodeFlowStateData(
                        new TaskStartData(
                            [.. state.Inputs.Values.Select(input => new StartInputData(input.Port.Value, input.Text))],
                            ToData(state.StartTexts.Texts)),
                        ToData(state.Values))),
            _ => throw new NotSupportedException($"未知的流程记录 {flow.Identity.Kind}。"),
        };

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

    private static InputValueData ToData(InputValueRecord value) =>
        new(value.Port.Value, ToData(value.Origin), value.Text);

    private static ContextSourceData ToData(ContextSourceRecord source) =>
        new(
            source.Kind,
            source.Execution?.Value,
            source.Node is { } node ? ToData(node) : null,
            source.Port?.Value);

    private static NodeData ToData(NodeRecord node) =>
        node switch
        {
            ExecutableNodeRecord executable => new NodeData(
                Kind: "Executable",
                Path: ToData(executable.Path),
                Common: ToData(executable.Common),
                Block: ToData(executable.Block),
                RerunTargets: [.. executable.RerunTargets.Select(ToData)],
                Waiting: null,
                Inputs: null,
                Awaiting: null),

            InputNodeRecord input => new NodeData(
                Kind: "Input",
                Path: ToData(input.Path),
                Common: ToData(input.Common),
                Block: null,
                RerunTargets: null,
                Waiting: input.Waiting,
                Inputs: null,
                Awaiting: null),
            ApprovalNodeRecord approval => new NodeData(
                Kind: "Approval",
                Path: ToData(approval.Path),
                Common: ToData(approval.Common),
                Block: null,
                RerunTargets: null,
                Waiting: null,
                Inputs: ToData(approval.Inputs),
                Awaiting: approval.Awaiting),
            ContainerNodeRecord container => new NodeData(
                Kind: "Container",
                Path: ToData(container.Path),
                Common: ToData(container.Common),
                Block: null,
                RerunTargets: null,
                Waiting: null,
                Inputs: null,
                Awaiting: null),
            _ => throw new NotSupportedException($"不认识的节点记录 {node.GetType().Name}。"),
        };

    private static NodeCommonData ToData(NodeCommonRecord common) =>
        new(
            common.Revision,
            common.Executions,
            common.LastInputGroup,
            [.. common.Consumed.Select(entry => new ConsumedRevisionData(
                entry.Source is { } source ? ToData(source) : null,
                entry.Port?.Value,
                entry.Revision))],
            common.ActivePublicationRevision,
            common.ForceRerun);

    private static PortPublicationData ToData(PortPublicationRecord record) =>
        new(
            ToData(record.Node),
            record.Port.Value,
            record.Revision,
            record.At,
            record.Trigger,
            [.. record.Values.Select(value => new PublicationValueData(ToData(value.Origin), value.Text))]);

    private static NodeBlockData? ToData(NodeBlock? block) =>
        block is null
            ? null
            : new NodeBlockData(block.Summary, block.Execution?.Value, block.At);

    private static NodeInputValuesData ToData(NodeInputValuesRecord inputs) =>
        new(
            [.. inputs.Ordered.Select(item => new NodeInputValueData(
                item.Target.Value,
                item.Type,
                ToData(item.Value)))]);

    private static NodePathData ToData(NodePath path) =>
        new([.. path.Levels.Select(level => level.Value)]);

    private static Dictionary<string, string> ToData(IReadOnlyDictionary<PortName, string> values) =>
        values.ToDictionary(pair => pair.Key.Value, pair => pair.Value);

    private static Dictionary<string, string> ToData(IReadOnlyDictionary<string, string> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static TaskRecord ReadTask(TaskRecordData? dto)
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

    private static FlowRecord ReadFlow(FlowData dto)
    {
        string kind = RequiredText(dto.Kind, "流程种类");
        FlowIdentity identity = new(
            new FlowName(RequiredText(dto.Name, "流程名")),
            kind,
            RequiredText(dto.Implementation, "实现标识"));

        return kind switch
        {
            FlowRecordKinds.Graph => new FlowRecord(
                identity,
                ReadGraphDefinition(dto.GraphDefinition),
                ReadGraphState(dto.GraphState)),
            FlowRecordKinds.Code => new FlowRecord(
                identity,
                ReadCodeDefinition(dto.CodeDefinition),
                ReadCodeState(dto.CodeState)),
            _ => throw new FormatException($"未知的流程种类 {kind}。"),
        };
    }

    private static GraphFlowDefinitionRecord ReadGraphDefinition(GraphFlowDefinitionData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("存档缺少图流程定义。");
        }

        return new GraphFlowDefinitionRecord(
            new FlowName(RequiredText(dto.Name, "流程名")),
            RequiredText(dto.Nodes, "节点库文档"),
            RequiredText(dto.Flow, "流程文档"));
    }

    private static GraphFlowStateRecord ReadGraphState(GraphFlowStateData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("存档缺少图流程状态。");
        }

        TaskStartData start = dto.Start ?? throw new FormatException("存档缺少开始状态。");
        SchedulerSeedData pending = dto.Pending ?? throw new FormatException("存档缺少调度种子。");

        return new GraphFlowStateRecord(
            new TaskStartSnapshot(
                new StartInputs(
                [
                    .. (start.Inputs ?? []).Select(input => new StartInput(
                        ReadPort(input.Port, "开始输入端口"),
                        RequiredText(input.Text, "开始输入文本"))),
                ]),
                ReadTexts(start.StartTexts)),
            [.. (dto.Nodes ?? []).Select(ReadNode)],
            [.. (dto.Ports ?? []).Select(ReadPortPublication)],
            new FlowSeed([.. (pending.Targets ?? []).Select(path => new FlowStepPath([.. ReadPath(path, "调度目标").Levels.Select(level => level.Value)]))]));
    }

    private static CodeFlowDefinitionRecord ReadCodeDefinition(CodeFlowDefinitionData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("存档缺少代码流程定义。");
        }

        return new CodeFlowDefinitionRecord(
            RequiredText(dto.Implementation, "代码流程实现"),
            dto.Values ?? []);
    }

    private static CodeFlowStateRecord ReadCodeState(CodeFlowStateData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("存档缺少代码流程状态。");
        }

        TaskStartData start = dto.Start ?? throw new FormatException("存档缺少开始状态。");

        return new CodeFlowStateRecord(
            new StartInputs(
            [
                .. (start.Inputs ?? []).Select(input => new StartInput(
                    ReadPort(input.Port, "开始输入端口"),
                    RequiredText(input.Text, "开始输入文本"))),
            ]),
            new FlowTexts(ReadTexts(start.StartTexts)),
            dto.Values ?? []);
    }

    private static ExecutionRecord ReadExecution(ExecutionData dto) =>
        new(
            new RecordExecutionId(dto.Id),
            new Kuroe.Flow.Execution.ExecutionTargetId(RequiredText(dto.Target, "执行目标")),
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
            RequiredText(dto.Source, "输入来源"),
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

    private static InputValueRecord ReadInputValue(InputValueData dto) =>
        new(
            ReadPort(dto.Port, "输入端口"),
            ReadContextSource(dto.Origin),
            dto.Text ?? string.Empty);

    private static ContextSourceRecord ReadContextSource(ContextSourceData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("输入取值缺少出处。");
        }

        return new ContextSourceRecord(
            dto.Kind,
            dto.Execution is { } execution ? new ExecutionId(execution) : null,
            dto.Node is null ? null : ReadPath(dto.Node, "出处节点"),
            dto.Port is null ? null : new PortName(dto.Port));
    }

    private static IReadOnlyList<JournalEntry> ReadJournal(List<JournalEntryData>? entries) =>
        entries is null
            ? []
            : [.. entries.Select(entry => new JournalEntry(
                entry.At,
                entry.Level,
                RequiredText(entry.Label, "过程记录标签"),
                RequiredText(entry.Text, "过程记录文本")))];

    private static NodeRecord ReadNode(NodeData dto)
    {
        NodePath path = ReadPath(dto.Path, "节点路径");
        NodeCommonRecord common = ReadCommon(dto.Common);
        NodeBlock? block = ReadBlock(dto.Block);

        return dto.Kind switch
        {
            "Executable" => new ExecutableNodeRecord(
                path,
                common,
                [.. (dto.RerunTargets ?? []).Select(target => ReadPath(target, "重新执行目标"))],
                block),
            "Input" => new InputNodeRecord(
                path,
                common,
                dto.Waiting ?? false),
            "Approval" => new ApprovalNodeRecord(
                path,
                common,
                ReadNodeInputs(dto.Inputs),
                dto.Awaiting ?? false),
            "Container" => new ContainerNodeRecord(
                path,
                common),
            _ => throw new FormatException($"未知的节点记录种类 {dto.Kind}。"),
        };
    }

    private static NodeBlock? ReadBlock(NodeBlockData? dto)
    {
        if (dto is null)
        {
            return null;
        }

        ExecutionId? execution = dto.Execution is { } id ? new ExecutionId(id) : null;
        DateTimeOffset at = dto.At ?? throw new FormatException("节点阻塞缺少时刻。");

        return new NodeBlock(RequiredText(dto.Summary, "阻塞原因"), execution, at);
    }

    private static NodeCommonRecord ReadCommon(NodeCommonData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("节点记录缺少公共状态。");
        }

        return new NodeCommonRecord(
            dto.Revision,
            dto.Executions,
            dto.LastInputGroup,
            [.. (dto.Consumed ?? []).Select(entry => new ConsumedRevisionRecord(
                entry.Source is null ? null : ReadPath(entry.Source, "已消费来源"),
                entry.Port is null ? null : new PortName(entry.Port),
                entry.Revision))],
            dto.ActivePublicationRevision,
            dto.ForceRerun);
    }

    private static PortPublicationRecord ReadPortPublication(PortPublicationData dto) =>
        new(
            ReadPath(dto.Node, "端口记录节点"),
            ReadPort(dto.Port, "端口记录端口"),
            dto.Revision,
            dto.At,
            dto.Trigger,
            [
                .. (dto.Values ?? []).Select(value => new PublicationValueRecord(
                    ReadContextSource(value.Origin),
                    value.Text ?? string.Empty)),
            ]);

    private static NodeInputValuesRecord ReadNodeInputs(NodeInputValuesData? dto)
    {
        if (dto is null)
        {
            throw new FormatException("审批节点记录缺少输入取值。");
        }

        return new NodeInputValuesRecord(
            [.. (dto.Ordered ?? []).Select(item => new NodeInputValueRecord(
                ReadPort(item.Target, "审批输入端口"),
                RequiredText(item.Type, "审批输入类型"),
                ReadInputValue(item.Value ?? throw new FormatException("审批输入缺少取值。"))))]);
    }

    private static Dictionary<PortName, string> ReadTexts(Dictionary<string, string>? values) =>
        values is null
            ? []
            : values.ToDictionary(pair => new PortName(pair.Key), pair => pair.Value);

    private static TaskId ReadId(string? text) =>
        TaskId.TryParse(text, out TaskId id)
            ? id
            : throw new FormatException($"任务键不合法：{text ?? "空"}。");

    private static PortName ReadPort(string? text, string field) =>
        new(RequiredText(text, field));

    private static NodePath ReadPath(NodePathData? dto, string field)
    {
        if (dto?.Levels is null || dto.Levels.Count == 0)
        {
            throw new FormatException($"存档缺少{field}。");
        }

        return new NodePath([.. dto.Levels.Select(level => new NodeLabel(level))]);
    }

    private static string RequiredText(string? text, string field) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new FormatException($"存档缺少{field}。")
            : text;
}
