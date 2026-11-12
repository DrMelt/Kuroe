using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Graph.Records;

/// <summary>图驱动流程的持久定义：流程名、节点库文本与流程文本。</summary>
public sealed record GraphFlowDefinitionRecord(FlowName Name, string Nodes, string Flow) : IFlowDefinitionRecord;

/// <summary>图驱动流程的持久状态：开始快照、节点状态、端口发布历史与调度种子。</summary>
public sealed record GraphFlowStateRecord(
    TaskStartSnapshot Start,
    IReadOnlyList<NodeRecord> Nodes,
    IReadOnlyList<PortPublicationRecord> Ports,
    SchedulerSeed Pending) : IFlowStateRecord;

/// <summary>任务开始时锁定的输入：提交的开始输入与宿主开始端口文本。</summary>
public sealed record TaskStartSnapshot(
    StartInputs Inputs,
    IReadOnlyDictionary<PortName, string> StartTexts);

/// <summary>按流程名捕获当前流程文档快照。由宿主实现。</summary>
public interface IFlowSnapshotSource
{
    /// <summary>捕获指定流程的快照。</summary>
    ErrorOr<GraphFlowDefinitionRecord> Capture(FlowName name);
}

/// <summary>把流程快照重新编译为可运行流程。由宿主实现。</summary>
public interface IFlowSnapshotCompiler
{
    /// <summary>编译快照，失败时返回流程错误。</summary>
    ErrorOr<CompiledFlow> Compile(GraphFlowDefinitionRecord snapshot);
}
