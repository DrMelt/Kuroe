using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Flow;

/// <summary>任务流程模板：开始的输出端口与根节点，根节点通常是承载流程全部内容的容器。
/// 开始是流程的唯一来源，动态端口经 From 引用 Start@端口 取用初始输入，常量端口经 Bindings 读取。
/// 流程只声明开始端口，其中哪些由宿主给值由宿主开始输入表决定，不属于流程模型。</summary>
public sealed record FlowDefinition(
    FlowName Name,
    string? Description,
    IReadOnlyList<Port> StartOutputs,
    NodeSpec RootNode);