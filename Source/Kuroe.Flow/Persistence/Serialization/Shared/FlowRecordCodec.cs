using System.Text.Json;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>流程记录在存档中的定义与状态片段。片段由流程实现自己写出与读取。</summary>
public sealed record FlowRecordData(JsonElement Definition, JsonElement State);

/// <summary>流程记录的存档编解码端口：按流程身份认领，定义与状态各自编解码为存档片段。</summary>
public interface IFlowRecordCodec
{
    /// <summary>是否负责该流程身份的存档。</summary>
    bool Handles(FlowIdentity identity);

    /// <summary>从存档片段还原流程定义记录。片段不合法时抛出 FormatException。</summary>
    IFlowDefinitionRecord ReadDefinition(FlowRecordData data);

    /// <summary>从存档片段还原流程状态记录。片段不合法时抛出 FormatException。</summary>
    IFlowStateRecord ReadState(FlowRecordData data);

    /// <summary>把流程定义与状态写成存档片段。记录类型不受支持时抛出 NotSupportedException。</summary>
    FlowRecordData Write(IFlowDefinitionRecord definition, IFlowStateRecord state);
}