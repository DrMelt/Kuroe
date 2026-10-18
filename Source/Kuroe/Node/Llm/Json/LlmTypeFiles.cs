using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Node.Llm.Json;

/// <summary>Llm 节点文件形状的注册入口。</summary>
public static class LlmTypeFiles
{
    /// <summary>把 Llm 节点的文件形状注册进注册表。</summary>
    public static void RegisterInto(NodeTypeFileRegistry files) => files.Register(new LlmTypeFile());
}