using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Control.Graph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Node.StaticGraph.Graph;
using Kuroe.Flow.Node.StaticGraph.Ports;

namespace Kuroe.Flow.Control.Graph.Tasks;

/// <summary>任务快照的只读投影：按已实例化节点读出执行节点与容器的一刻状态。
/// 投影不实例化节点，未实例化的节点按默认待办报告。</summary>
internal sealed class TaskProjection(
    NodeGraph graph,
    NodeMaterializer materializer,
    StaticPortValues staticPorts)
{
    /// <summary>任务流程视图：步骤与分组来自执行节点与容器的当前状态，名称与说明来自流程模板。</summary>
    public IFlowView View(FlowDefinition flow, IReadOnlyList<GraphExecutionSnapshot> executions)
    {
        Dictionary<NodePath, List<GraphExecutionSnapshot>> byNode = [];
        foreach (GraphExecutionSnapshot execution in executions)
        {
            if (!byNode.TryGetValue(execution.Input.Node, out List<GraphExecutionSnapshot>? values))
            {
                values = [];
                byNode[execution.Input.Node] = values;
            }

            values.Add(execution);
        }

        return new FlowView(
            flow.Name.Value,
            "graph",
            flow.Description,
            [.. graph.StartPorts.Select(Output)],
            [.. graph.ExecutableNodes.Select(node => Step(node, byNode.GetValueOrDefault(node.Path) ?? []))],
            [.. graph.Containers.Select(Group)]);
    }

    /// <summary>执行节点在快照里的一刻状态：节点自己给出，未实例化的节点按默认待办报告。阻塞节点的可恢复标记来自恢复计划缓存。</summary>
    private ExecutableStateSnapshot State(NodePath path) =>
        materializer.Find(path)?.Snapshot() ?? new ExecutableStateSnapshot(path, NodeState.Pending);

    private FlowStepView Step(ExecutableNode node, IReadOnlyList<GraphExecutionSnapshot> executions)
    {
        ExecutableStateSnapshot state = State(node.Path);
        IPortTexts staticTexts = staticPorts.For(node);
        IPortTexts constantTexts = staticPorts.ConstantsFor(node);

        return new FlowStepView(
            node.Path.Value,
            node.Label.Value,
            node.Kind.Value,
            StateOf(state.State),
            [.. node.Inputs.Select(port => Input(port, staticTexts))],
            [.. node.Outputs.Select(port => Output(port, constantTexts))],
            [.. executions.Select(Execution)],
            LimitOf(node.Path) ?? node.MaxExecutions)
        {
            ParentPath = node.Path.Parent?.Value,
            ExecutionCount = materializer.Find(node.Path)?.Progress.Executions ?? 0,
            CanRestore = state.CanRestore,
            Block = state.Block?.Summary,
        };
    }

    /// <summary>容器在快照里的一刻状态：成员活动、输出与发布。成员状态只在已实例化成员上读，未实例化成员视为没有活动。</summary>
    private FlowGroupView Group(ContainerNode container)
    {
        NodeState state;
        int publicationCount;
        if (materializer.Find(container.Path) is RuntimeContainer runtime)
        {
            if (runtime.Canceled)
            {
                state = NodeState.Canceled;
            }
            else if (runtime.Released)
            {
                state = NodeState.Done;
            }
            else
            {
                state = ActivityState(container);
            }

            publicationCount = runtime.Progress.Executions;
        }
        else
        {
            state = ActivityState(container);
            publicationCount = 0;
        }

        return new FlowGroupView(
            container.Path.Value,
            container.Path.Label.Value,
            ChildPaths(container))
        {
            ParentPath = container.Path.Parent?.Value,
            State = StateOf(state),
            Outputs = [.. container.Outputs.Select(Output)],
            MaxExecutions = LimitOf(container.Path) ?? container.MaxExecutions,
            PublicationCount = publicationCount,
        };
    }

    private static FlowNodeState StateOf(NodeState state) => state switch
    {
        NodeState.Running => FlowNodeState.Running,
        NodeState.Blocked => FlowNodeState.Blocked,
        NodeState.Done => FlowNodeState.Done,
        NodeState.Canceled => FlowNodeState.Canceled,
        _ => FlowNodeState.Pending,
    };

    private static FlowPortView Input(InputPort port, IPortTexts staticTexts) => new(
        port.Name.Value,
        port.Type.Name,
        staticTexts.Text(port.Name) ?? port.Description ?? port.Default,
        port.Trigger);

    private static FlowPortView Output(Port port) => new(
        port.Name.Value,
        port.Type.Name,
        port.Description ?? port.Value,
        port.Trigger);

    private static FlowPortView Output(Port port, IPortTexts constantTexts) => new(
        port.Name.Value,
        port.Type.Name,
        constantTexts.Text(port.Name) ?? port.Description ?? port.Value,
        port.Trigger)
    {
        Constant = constantTexts.Text(port.Name) is not null,
    };

    private static FlowExecutionView Execution(GraphExecutionSnapshot execution) => new(
        execution.Id.Value,
        (Kuroe.Flow.Execution.ExecutionState)execution.State,
        execution.Progress,
        [])
    {
        Round = execution.Input.ExecutionCount,
        CreatedAt = execution.CreatedAt,
        StartedAt = execution.StartedAt,
        FinishedAt = execution.FinishedAt,
        Failures = execution.Failures,
        HasRestoreData = execution.HasRestoreData,
        ContinuedFrom = execution.ContinuedFrom?.Value,
    };

    /// <summary>方案给出的执行或发布次数上限，没有方案时为空。</summary>
    private int? LimitOf(NodePath path) => graph.PlanOf(path)?.MaxExecutions;

    /// <summary>容器的直接子节点路径，先根序。</summary>
    private static IReadOnlyList<string> ChildPaths(ContainerNode container) =>
        [.. container.Children.Select(child => child.Path.Value)];

    /// <summary>容器里已实例化成员的活动状态：有在跑或已产出即运行中，都没有即待办。</summary>
    private NodeState ActivityState(ContainerNode container) =>
        graph.ExecutablesIn(container.Path).Any(MemberActive) ? NodeState.Running : NodeState.Pending;

    /// <summary>成员执行节点是否有在跑的执行或已产出，未实例化的成员没有活动。</summary>
    private bool MemberActive(NodePath executable) =>
        materializer.Find(executable) is RuntimeExecutable node
            && (node.HasActive || node.Progress.Published);
}