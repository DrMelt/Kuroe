using Kuroe.Flow.Persistence.Code;
using Kuroe.Flow.Persistence.Graph;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Persistence.Serialization.Shared;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的存档编解码装配：图流程与代码流程的片段编解码器全部注册。</summary>
public static class TaskRecordCodecs
{
    /// <summary>建立带图与代码编解码器的存档映射。</summary>
    public static TaskRecordSerializationMapper Mapper() =>
        new([new GraphFlowRecordCodec(), new CodeFlowRecordCodec()]);

    /// <summary>建立带图与代码编解码器的 JSON 存档编解码。</summary>
    public static TaskRecordCodec Codec() => new(Mapper());
}