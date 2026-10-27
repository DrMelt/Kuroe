using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Shared.Tools;

namespace Kuroe.Node.Llm;

/// <summary>Llm 节点类型的对外入口：类型名、静态形状与实例建立。Llm 节点由模型执行，收口时交回端口输出。</summary>
public static class LlmNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Llm");

    /// <summary>Llm 类型的静态形状：派发执行，输入与输出端口取文本、上下文与信号，Text 与 Signal 另有 Trigger 版，
    /// Trigger 版交回时携带是否触发下游的布尔值。</summary>
    public static NodeTypeShape Shape { get; } = new()
    {
        DispatchesExecutions = true,
        InputTypes = [PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger],
        OutputTypes = [PortTypes.Text, PortTypes.TextTrigger, PortTypes.Context, PortTypes.Signal, PortTypes.SignalTrigger],
    };

    /// <summary>按类型专属声明与工具契约建出类型实例，未声明时取默认声明。</summary>
    public static INodeType Create(LlmParameters? parameters, IToolContractCatalog? contracts = null) =>
        new LlmNodeType(parameters, contracts);
}