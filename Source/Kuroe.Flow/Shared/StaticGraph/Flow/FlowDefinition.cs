using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Flow;

/// <summary>任务流程模板：开始的输出端口、命名的模型选择与根节点，根节点通常是承载流程全部内容的容器。
/// 开始是流程的唯一来源，一切节点经 From 引用 Start@端口 取用初始输入。装配与查询由 StaticGraph 域负责。
/// HostStartInputs 是开始端口到宿主输入名的绑定，绑定端口的值由宿主在任务启动时给出，不接受提交输入。</summary>
public sealed record FlowDefinition(
    FlowName Name,
    string? Description,
    IReadOnlyList<ModelDefinition> Models,
    IReadOnlyList<Port> StartOutputs,
    IReadOnlyDictionary<PortName, string> HostStartInputs,
    NodeSpec RootNode);
