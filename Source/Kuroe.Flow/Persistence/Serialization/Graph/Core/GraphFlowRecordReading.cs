using Kuroe.Flow.Execution.Shared;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Persistence.Graph;

/// <summary>存档形状到图流程记录的映射。</summary>
internal static class GraphFlowRecordReading
{
    public static GraphFlowDefinitionRecord ReadDefinition(GraphFlowDefinitionData dto) =>
        new(
            new FlowName(RequiredText(dto.Name, "流程名")),
            RequiredText(dto.Nodes, "节点库文档"),
            RequiredText(dto.Flow, "流程文档"));

    public static GraphFlowStateRecord ReadState(GraphFlowStateData dto)
    {
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

    private static Dictionary<PortName, string> ReadTexts(Dictionary<string, string>? values) =>
        values is null
            ? []
            : values.ToDictionary(pair => new PortName(pair.Key), pair => pair.Value);

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