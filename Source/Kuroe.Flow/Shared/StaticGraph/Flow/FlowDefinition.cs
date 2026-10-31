using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Flow;

/// <summary>任务流程模板：开始的输出端口与根节点，根节点通常是承载流程全部内容的容器。
/// 开始是流程的唯一来源，动态端口经 From 引用 Start@端口 取用初始输入，常量端口经 Static 读取。
/// HostStartInputs 是开始端口到宿主输入名的绑定，绑定端口的值由宿主在任务启动时给出，不接受提交输入。</summary>
public sealed record FlowDefinition(
    FlowName Name,
    string? Description,
    IReadOnlyList<Port> StartOutputs,
    IReadOnlyDictionary<PortName, string> HostStartInputs,
    NodeSpec RootNode);
