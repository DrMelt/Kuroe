using ErrorOr;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Kuroe.Maf.Records.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Persistence;

/// <summary>MAF 流程记录的存档编解码：定义与状态各自成为一段 JSON 片段。</summary>
public sealed class MafFlowRecordCodec : IFlowRecordCodec
{
    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) => identity.Kind == MafFlowKind.Value;

    /// <inheritdoc/>
    public ErrorOr<IFlowDefinitionRecord> ReadDefinition(FlowRecordData data)
    {
        try
        {
            MafFlowDefinitionData dto = Read(
                data.Definition,
                MafFlowRecordJson.Default.MafFlowDefinitionData,
                "MAF 流程定义");

            return new MafFlowDefinitionRecord(
                RequiredText(dto.Implementation, "MAF 流程实现"),
                RequiredText(dto.Flow, "MAF 流程名"));
        }
        catch (FormatException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, ex.Message)];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<IFlowStateRecord> ReadState(FlowRecordData data)
    {
        try
        {
            MafFlowStateData dto = Read(
                data.State,
                MafFlowRecordJson.Default.MafFlowStateData,
                "MAF 流程状态");
            TaskStartData start = dto.Start ?? throw new FormatException("存档缺少开始状态。");

            return new MafFlowStateRecord(
                new StartInputs(
                [
                    .. (start.Inputs ?? []).Select(input => new StartInput(
                        new PortName(RequiredText(input.Port, "开始输入端口")),
                        RequiredText(input.Text, "开始输入文本"))),
                ]),
                new FlowTexts(ReadTexts(start.StartTexts)),
                dto.Model,
                dto.CheckpointSession,
                dto.CheckpointId,
                dto.CheckpointData,
                dto.PendingSteps ?? []);
        }
        catch (FormatException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, ex.Message)];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<FlowRecordData> Write(IFlowDefinitionRecord definition, IFlowStateRecord state)
    {
        if (definition is not MafFlowDefinitionRecord mafDefinition
            || state is not MafFlowStateRecord mafState)
        {
            return [Error.Failure(FlowErrorCodes.RecordWrite, "MAF 流程编解码收到非 MAF 流程记录。")];
        }

        return new FlowRecordData(
            JsonSerializer.SerializeToElement(
                new MafFlowDefinitionData(mafDefinition.Implementation, mafDefinition.Flow),
                MafFlowRecordJson.Default.MafFlowDefinitionData),
            JsonSerializer.SerializeToElement(
                new MafFlowStateData(
                    new TaskStartData(
                        [.. mafState.Inputs.Values.Select(input => new StartInputData(input.Port.Value, input.Text))],
                        mafState.StartTexts.Texts.ToDictionary(pair => pair.Key.Value, pair => pair.Value)),
                    mafState.Model,
                    mafState.CheckpointSession,
                    mafState.CheckpointId,
                    mafState.CheckpointData,
                    [.. mafState.PendingSteps]),
                MafFlowRecordJson.Default.MafFlowStateData));
    }

    private static T Read<T>(JsonElement element, JsonTypeInfo<T> info, string field)
    {
        try
        {
            return element.Deserialize(info) ?? throw new FormatException($"存档缺少{field}。");
        }
        catch (JsonException ex)
        {
            throw new FormatException($"{field}不合法：{ex.Message}");
        }
    }

    private static Dictionary<PortName, string> ReadTexts(Dictionary<string, string>? values) =>
        values is null
            ? []
            : values.ToDictionary(pair => new PortName(pair.Key), pair => pair.Value);

    private static string RequiredText(string? text, string field) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new FormatException($"存档缺少{field}。")
            : text;
}