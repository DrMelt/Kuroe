using ErrorOr;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Tasks.Shared;
using System.Text.Json;
using Kuroe.Tasks.Records.Shared;

namespace Kuroe.Flow.Persistence.Graph;

/// <summary>图流程记录的存档编解码：定义与状态各自成为一段 JSON 片段。</summary>
public sealed class GraphFlowRecordCodec : IFlowRecordCodec
{
    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) => identity.Kind == GraphFlowKind.Value;

    /// <inheritdoc/>
    public ErrorOr<IFlowDefinitionRecord> ReadDefinition(FlowRecordData data)
    {
        try
        {
            return GraphFlowRecordReading.ReadDefinition(Read<GraphFlowDefinitionData>(
                data.Definition,
                GraphFlowRecordJson.Default.GraphFlowDefinitionData,
                "图流程定义"));
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
            return GraphFlowRecordReading.ReadState(Read<GraphFlowStateData>(
                data.State,
                GraphFlowRecordJson.Default.GraphFlowStateData,
                "图流程状态"));
        }
        catch (FormatException ex)
        {
            return [Error.Failure(FlowErrorCodes.RecordFormat, ex.Message)];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<FlowRecordData> Write(IFlowDefinitionRecord definition, IFlowStateRecord state)
    {
        if (definition is not GraphFlowDefinitionRecord graphDefinition
            || state is not GraphFlowStateRecord graphState)
        {
            return [Error.Failure(FlowErrorCodes.RecordWrite, "图流程编解码收到非图流程记录。")];
        }

        return new FlowRecordData(
            JsonSerializer.SerializeToElement(
                GraphFlowRecordMapping.ToData(graphDefinition),
                GraphFlowRecordJson.Default.GraphFlowDefinitionData),
            JsonSerializer.SerializeToElement(
                GraphFlowRecordMapping.ToData(graphState),
                GraphFlowRecordJson.Default.GraphFlowStateData));
    }

    private static T Read<T>(JsonElement element, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, string field)
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
}