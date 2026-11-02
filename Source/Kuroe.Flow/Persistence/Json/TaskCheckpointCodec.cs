using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Persistence.Json;

/// <summary>检查点的 JSON 编解码。DTO 与领域模型分离，读取逐层校验，写出的字段顺序固定。</summary>
public sealed class TaskCheckpointCodec : ITaskCheckpointCodec
{
    /// <inheritdoc/>
    public ErrorOr<TaskCheckpointDocument> Read(string text)
    {
        int version;
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (ReadVersion(document.RootElement) is not { } declared)
            {
                return [Error.Failure(FlowErrorCodes.CheckpointFormat, "检查点缺少版本。")];
            }

            version = declared;
        }
        catch (JsonException ex)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointFormat, $"检查点 JSON 不合法：{ex.Message}")];
        }

        if (version != TaskCheckpointVersion.Current)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointVersion, $"不支持的检查点版本 {version}。")];
        }

        TaskCheckpointFileDto? file;
        try
        {
            file = JsonSerializer.Deserialize(text, TaskCheckpointJson.Default.TaskCheckpointFileDto);
        }
        catch (JsonException ex)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointFormat, $"检查点 JSON 不合法：{ex.Message}")];
        }

        if (file is null)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointFormat, "检查点为空。")];
        }

        try
        {
            return new TaskCheckpointDocument(version, ReadTask(file.Task));
        }
        catch (CheckpointFormatException ex)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointFormat, ex.Message)];
        }
        catch (ArgumentException ex)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointFormat, ex.Message)];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<string> Write(TaskCheckpointDocument document)
    {
        try
        {
            return JsonSerializer.Serialize(ToDto(document), TaskCheckpointJson.WriteOptions.GetTypeInfo(typeof(TaskCheckpointFileDto)));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return [Error.Failure(FlowErrorCodes.CheckpointWrite, $"写出检查点失败：{ex.Message}")];
        }
    }

    /// <summary>在严格映射字段之前读取版本，旧版本按版本不符拒绝，不误报为格式损坏。</summary>
    private static int? ReadVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(TaskCheckpointFileDto.Version), StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out int version))
            {
                return version;
            }
        }

        return null;
    }

    private static TaskCheckpointFileDto ToDto(TaskCheckpointDocument document) =>
        new()
        {
            Version = document.Version,
            Task = new TaskCheckpointDto
            {
                Identity = new TaskIdentityDto
                {
                    Id = document.Task.Identity.Id.Value,
                    Title = document.Task.Identity.Title,
                    CreatedAt = document.Task.Identity.CreatedAt,
                    LastActivityAt = document.Task.Identity.LastActivityAt,
                },
                Flow = new FlowSnapshotDto
                {
                    Name = document.Task.Flow.Name.Value,
                    Nodes = document.Task.Flow.Nodes,
                    Flow = document.Task.Flow.Flow,
                },
                Start = new TaskStartDto
                {
                    Inputs =
                    [
                        .. document.Task.Start.Inputs.Values.Select(input => new StartInputDto
                        {
                            Port = input.Port.Value,
                            Text = input.Text,
                        }),
                    ],
                    StartTexts = ToDto(document.Task.Start.StartTexts),
                },
                Lifecycle = document.Task.Lifecycle,
                Completed = document.Task.Completed,
                Journal = new TaskJournalDto
                {
                    Entries = [.. document.Task.Journal.Entries.Select(ToDto)],
                    DroppedEntries = document.Task.Journal.DroppedEntries,
                },
                Executions = [.. document.Task.Executions.Select(ToDto)],
                Nodes = [.. document.Task.Nodes.Select(ToDto)],
                Pending = new SchedulerSeedDto
                {
                    Targets = [.. document.Task.Pending.Targets.Select(ToDto)],
                },
            },
        };

    private static JournalDto ToDto(JournalEntry entry) =>
        new()
        {
            At = entry.At,
            Level = entry.Level,
            Label = entry.Label,
            Text = entry.Text,
        };

    private static ExecutionDto ToDto(ExecutionCheckpoint execution) =>
        new()
        {
            Id = execution.Id.Value,
            Node = ToDto(execution.Node),
            ExecutionCount = execution.ExecutionCount,
            State = execution.State,
            Progress = execution.Progress,
            CreatedAt = execution.CreatedAt,
            StartedAt = execution.StartedAt,
            FinishedAt = execution.FinishedAt,
            Failures = [.. execution.Failures],
            Journal = [.. execution.Journal.Select(ToDto)],
            DroppedEntries = execution.DroppedEntries,
            Inputs = [.. execution.Inputs.Select(ToDto)],
            Texts = ToDto(execution.Texts),
        };

    private static InputValueDto ToDto(InputValueCheckpoint value) =>
        new()
        {
            Port = value.Port.Value,
            Origin = ToDto(value.Origin),
            Text = value.Text,
        };

    private static ContextSourceDto ToDto(ContextSourceCheckpoint source) =>
        new()
        {
            Kind = source.Kind,
            Execution = source.Execution?.Value,
            Node = source.Node is { } node ? ToDto(node) : null,
            Port = source.Port?.Value,
        };

    private static NodeDto ToDto(NodeCheckpoint node) =>
        node switch
        {
            ExecutableNodeCheckpoint executable => new NodeDto
            {
                Kind = "Executable",
                Path = ToDto(executable.Path),
                Common = ToDto(executable.Common),
                ReworkTargets = [.. executable.ReworkTargets.Select(ToDto)],
                Blocked = executable.Blocked,
            },
            SelfDrivenNodeCheckpoint selfDriven => new NodeDto
            {
                Kind = "SelfDriven",
                Path = ToDto(selfDriven.Path),
                Common = ToDto(selfDriven.Common),
                Failure = selfDriven.Failure,
                FailureTaken = selfDriven.FailureTaken,
            },
            InputNodeCheckpoint input => new NodeDto
            {
                Kind = "Input",
                Path = ToDto(input.Path),
                Common = ToDto(input.Common),
                Waiting = input.Waiting,
            },
            ApprovalNodeCheckpoint approval => new NodeDto
            {
                Kind = "Approval",
                Path = ToDto(approval.Path),
                Common = ToDto(approval.Common),
                Inputs = ToDto(approval.Inputs),
                Awaiting = approval.Awaiting,
            },
            ContainerNodeCheckpoint container => new NodeDto
            {
                Kind = "Container",
                Path = ToDto(container.Path),
                Common = ToDto(container.Common),
            },
            _ => throw new NotSupportedException($"不认识的节点检查点 {node.GetType().Name}。"),
        };

    private static NodeCommonDto ToDto(NodeCommonCheckpoint common) =>
        new()
        {
            Revision = common.Revision,
            Executions = common.Executions,
            LastInputGroup = common.LastInputGroup,
            Consumed =
            [
                .. common.Consumed.Select(entry => new ConsumedRevisionDto
                {
                    Source = ToDto(entry.Source),
                    Port = entry.Port?.Value,
                    Revision = entry.Revision,
                }),
            ],
            Publication = ToDto(common.Publication),
            ForceRerun = common.ForceRerun,
        };

    private static PublicationDto? ToDto(PublicationCheckpoint? publication) =>
        publication is null
            ? null
            : new PublicationDto
            {
                Triggers = publication.Triggers.ToDictionary(pair => pair.Key.Value, pair => pair.Value),
                Content = publication.Content.ToDictionary(
                    pair => pair.Key.Value,
                    pair => pair.Value
                        .Select(value => new PublicationValueDto
                        {
                            Origin = ToDto(value.Origin),
                            Text = value.Text,
                        })
                        .ToList()),
            };

    private static NodeInputValuesDto ToDto(NodeInputValuesCheckpoint inputs) =>
        new()
        {
            Ordered =
            [
                .. inputs.Ordered.Select(item => new NodeInputValueDto
                {
                    Target = item.Target.Value,
                    Type = item.Type,
                    Value = ToDto(item.Value),
                }),
            ],
            Connected = [.. inputs.Connected.Select(port => port.Value)],
            Triggers = inputs.Triggers.ToDictionary(pair => pair.Key.Value, pair => pair.Value),
            StaticTexts = ToDto(inputs.StaticTexts),
        };

    private static NodePathDto ToDto(NodePath path) => new() { Levels = [.. path.Levels.Select(level => level.Value)] };

    private static Dictionary<string, string> ToDto(IReadOnlyDictionary<PortName, string> values) =>
        values.ToDictionary(pair => pair.Key.Value, pair => pair.Value);

    /// <summary>检查点的字段缺失或取值不合法。</summary>
    public sealed class CheckpointFormatException(string message) : Exception(message);

    private static TaskCheckpoint ReadTask(TaskCheckpointDto? dto)
    {
        if (dto is null)
        {
            throw new CheckpointFormatException("检查点缺少任务。");
        }

        TaskIdentityDto identity = dto.Identity ?? throw new CheckpointFormatException("检查点缺少任务身份。");
        FlowSnapshotDto flow = dto.Flow ?? throw new CheckpointFormatException("检查点缺少流程快照。");
        TaskStartDto start = dto.Start ?? throw new CheckpointFormatException("检查点缺少开始状态。");
        TaskJournalDto journal = dto.Journal ?? throw new CheckpointFormatException("检查点缺少任务过程记录。");
        SchedulerSeedDto pending = dto.Pending ?? throw new CheckpointFormatException("检查点缺少调度种子。");

        return new TaskCheckpoint(
            new TaskIdentity(
                ReadId(identity.Id),
                RequiredText(identity.Title, "任务标题"),
                identity.CreatedAt,
                identity.LastActivityAt),
            new FlowSnapshot(
                new FlowName(RequiredText(flow.Name, "流程名")),
                RequiredText(flow.Nodes, "节点库文档"),
                RequiredText(flow.Flow, "流程文档")),
            new TaskStartSnapshot(
                new StartInputs(
                [
                    .. (start.Inputs ?? []).Select(input => new StartInput(
                        ReadPort(input.Port, "开始输入端口"),
                        RequiredText(input.Text, "开始输入文本"))),
                ]),
                ReadTexts(start.StartTexts)),
            dto.Lifecycle,
            dto.Completed,
            new TaskJournalSnapshot(ReadJournal(journal.Entries), journal.DroppedEntries),
            [.. (dto.Executions ?? []).Select(ReadExecution)],
            [.. (dto.Nodes ?? []).Select(ReadNode)],
            new SchedulerSeed([.. (pending.Targets ?? []).Select(path => ReadPath(path, "调度目标"))]));
    }

    private static ExecutionCheckpoint ReadExecution(ExecutionDto dto) =>
        new(
            new ExecutionId(dto.Id),
            ReadPath(dto.Node, "执行节点"),
            dto.ExecutionCount,
            dto.State,
            dto.Progress ?? string.Empty,
            dto.CreatedAt,
            dto.StartedAt,
            dto.FinishedAt,
            dto.Failures ?? [],
            ReadJournal(dto.Journal),
            dto.DroppedEntries,
            [.. (dto.Inputs ?? []).Select(ReadInputValue)],
            ReadTexts(dto.Texts));

    private static InputValueCheckpoint ReadInputValue(InputValueDto dto) =>
        new(
            ReadPort(dto.Port, "输入端口"),
            ReadContextSource(dto.Origin),
            dto.Text ?? string.Empty);

    private static ContextSourceCheckpoint ReadContextSource(ContextSourceDto? dto)
    {
        if (dto is null)
        {
            throw new CheckpointFormatException("输入取值缺少出处。");
        }

        return new ContextSourceCheckpoint(
            dto.Kind,
            dto.Execution is { } execution ? new ExecutionId(execution) : null,
            dto.Node is null ? null : ReadPath(dto.Node, "出处节点"),
            dto.Port is null ? null : new PortName(dto.Port));
    }

    private static IReadOnlyList<JournalEntry> ReadJournal(List<JournalDto>? entries) =>
        entries is null
            ? []
            : [.. entries.Select(entry => new JournalEntry(
                entry.At,
                entry.Level,
                RequiredText(entry.Label, "过程记录标签"),
                RequiredText(entry.Text, "过程记录文本")))];

    private static NodeCheckpoint ReadNode(NodeDto dto) =>
        dto.Kind switch
        {
            "Executable" => new ExecutableNodeCheckpoint(
                ReadPath(dto.Path, "节点路径"),
                ReadCommon(dto.Common),
                [.. (dto.ReworkTargets ?? []).Select(path => ReadPath(path, "返工目标"))],
                dto.Blocked ?? false),
            "SelfDriven" => new SelfDrivenNodeCheckpoint(
                ReadPath(dto.Path, "节点路径"),
                ReadCommon(dto.Common),
                dto.Failure,
                dto.FailureTaken ?? false),
            "Input" => new InputNodeCheckpoint(
                ReadPath(dto.Path, "节点路径"),
                ReadCommon(dto.Common),
                dto.Waiting ?? false),
            "Approval" => new ApprovalNodeCheckpoint(
                ReadPath(dto.Path, "节点路径"),
                ReadCommon(dto.Common),
                ReadNodeInputs(dto.Inputs),
                dto.Awaiting ?? false),
            "Container" => new ContainerNodeCheckpoint(
                ReadPath(dto.Path, "节点路径"),
                ReadCommon(dto.Common)),
            _ => throw new CheckpointFormatException($"未知的节点检查点种类 {dto.Kind}。"),
        };

    private static NodeCommonCheckpoint ReadCommon(NodeCommonDto? dto)
    {
        if (dto is null)
        {
            throw new CheckpointFormatException("节点检查点缺少公共状态。");
        }

        return new NodeCommonCheckpoint(
            dto.Revision,
            dto.Executions,
            dto.LastInputGroup,
            [.. (dto.Consumed ?? []).Select(entry => new ConsumedRevisionCheckpoint(
                ReadPath(entry.Source, "已消费来源"),
                entry.Port is null ? null : new PortName(entry.Port),
                entry.Revision))],
            ReadPublication(dto.Publication),
            dto.ForceRerun);
    }

    private static PublicationCheckpoint? ReadPublication(PublicationDto? dto) =>
        dto is null
            ? null
            : new PublicationCheckpoint(
                (dto.Triggers ?? []).ToDictionary(pair => new PortName(pair.Key), pair => pair.Value),
                (dto.Content ?? []).ToDictionary(
                    pair => new PortName(pair.Key),
                    pair => (IReadOnlyList<PublicationValueCheckpoint>)
                    [
                        .. pair.Value.Select(value => new PublicationValueCheckpoint(
                            ReadContextSource(value.Origin),
                            value.Text ?? string.Empty)),
                    ]));

    private static NodeInputValuesCheckpoint ReadNodeInputs(NodeInputValuesDto? dto)
    {
        if (dto is null)
        {
            throw new CheckpointFormatException("审批节点检查点缺少输入取值。");
        }

        return new NodeInputValuesCheckpoint(
            [.. (dto.Ordered ?? []).Select(item => new NodeInputValueCheckpoint(
                ReadPort(item.Target, "审批输入端口"),
                RequiredText(item.Type, "审批输入类型"),
                ReadInputValue(item.Value ?? throw new CheckpointFormatException("审批输入缺少取值。"))))],
            [.. (dto.Connected ?? []).Select(port => new PortName(port))],
            (dto.Triggers ?? []).ToDictionary(pair => new PortName(pair.Key), pair => pair.Value),
            ReadTexts(dto.StaticTexts));
    }

    private static Dictionary<PortName, string> ReadTexts(Dictionary<string, string>? values) =>
        values is null
            ? []
            : values.ToDictionary(pair => new PortName(pair.Key), pair => pair.Value);

    private static TaskId ReadId(string? text) =>
        TaskId.TryParse(text, out TaskId id)
            ? id
            : throw new CheckpointFormatException($"任务键不合法：{text ?? "空"}。");

    private static PortName ReadPort(string? text, string field) =>
        new(RequiredText(text, field));

    private static NodePath ReadPath(NodePathDto? dto, string field)
    {
        if (dto is null || dto.Levels.Count == 0)
        {
            throw new CheckpointFormatException($"检查点缺少{field}。");
        }

        return new NodePath([.. dto.Levels.Select(level => new NodeLabel(level))]);
    }

    private static string RequiredText(string? text, string field) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new CheckpointFormatException($"检查点缺少{field}。")
            : text;
}
