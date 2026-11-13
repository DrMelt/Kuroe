using System.Text.Json;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Persistence.Graph;

/// <summary>图流程记录的存档编解码：定义与状态各自成为一段 JSON 片段。</summary>
public sealed class GraphFlowRecordCodec : IFlowRecordCodec
{
    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) => identity.Kind == FlowRecordKinds.Graph;

    /// <inheritdoc/>
    public IFlowDefinitionRecord ReadDefinition(FlowRecordData data) =>
        GraphFlowRecordReading.ReadDefinition(Read<GraphFlowDefinitionData>(
            data.Definition,
            GraphFlowRecordJson.Default.GraphFlowDefinitionData,
            "图流程定义"));

    /// <inheritdoc/>
    public IFlowStateRecord ReadState(FlowRecordData data) =>
        GraphFlowRecordReading.ReadState(Read<GraphFlowStateData>(
            data.State,
            GraphFlowRecordJson.Default.GraphFlowStateData,
            "图流程状态"));

    /// <inheritdoc/>
    public FlowRecordData Write(IFlowDefinitionRecord definition, IFlowStateRecord state)
    {
        if (definition is not GraphFlowDefinitionRecord graphDefinition
            || state is not GraphFlowStateRecord graphState)
        {
            throw new NotSupportedException("图流程编解码收到非图流程记录。");
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