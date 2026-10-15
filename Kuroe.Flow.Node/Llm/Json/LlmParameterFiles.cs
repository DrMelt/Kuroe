using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Flow.Node.Llm.Json;

/// <summary>Llm 节点参数段形状的注册入口。</summary>
public static class LlmParameterFiles
{
    /// <summary>把 Llm 节点的参数段形状注册进注册表。</summary>
    public static void RegisterInto(NodeParameterFileRegistry files) => files.Register(new LlmParameterFile());
}