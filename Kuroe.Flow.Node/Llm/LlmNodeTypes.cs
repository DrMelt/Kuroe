using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Llm;

/// <summary>Llm 节点类型的对外入口：类型名与注册。Llm 节点由模型执行，收口时交回端口输出。</summary>
public static class LlmNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Llm");

    /// <summary>把 Llm 类型注册进注册表，实例按需建立。</summary>
    public static void RegisterInto(NodeTypeRegistry registry) => registry.Register(Name, () => new LlmNodeType());
}