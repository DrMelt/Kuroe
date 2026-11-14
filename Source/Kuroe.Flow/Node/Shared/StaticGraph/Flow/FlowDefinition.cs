using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.StaticGraph.Flow;

/// <summary>任务流程模板：流程级开始端口与根节点，根节点通常是承载流程全部内容的容器。
/// 开始端口不是节点，根作用域的 @端口 取用初始输入，常量端口经 Bindings 读取。
/// 流程只声明开始端口，其中哪些由宿主给值由宿主开始输入表决定，不属于流程模型。</summary>
public sealed record FlowDefinition(
    FlowName Name,
    string? Description,
    IReadOnlyList<Port> StartPorts,
    NodeSpec RootNode);