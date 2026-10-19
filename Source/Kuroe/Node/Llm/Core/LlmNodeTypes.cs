using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Node.Llm;

/// <summary>Llm 节点类型的对外入口：类型名、静态形状与实例建立。Llm 节点由模型执行，收口时交回端口输出。</summary>
public static class LlmNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Llm");

    /// <summary>Llm 类型的静态形状：派发执行，输入输出端口取值域由模型产出的形态决定。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        DispatchesExecutions = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
        OutputTypes = [PortType.Text, PortType.Context],
    };

    /// <summary>按类型专属声明建出类型实例，未声明时取默认声明。</summary>
    public static INodeType Create(LlmParameters? parameters) => new LlmNodeType(parameters);
}