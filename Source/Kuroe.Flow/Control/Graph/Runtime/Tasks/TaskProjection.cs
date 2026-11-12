using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务快照的只读投影：按已实例化节点读出执行节点与容器的一刻状态。
/// 投影不实例化节点，未实例化的节点按默认待办报告。</summary>
internal sealed class TaskProjection(
    NodeGraph graph,
    NodeMaterializer materializer,
    StaticPortValues staticPorts)
{
    /// <summary>节点在快照里的一刻状态：节点自己给出，未实例化的节点按默认待办报告。阻塞节点的可恢复标记来自恢复计划缓存。</summary>
    public ExecutableStateSnapshot State(NodePath path) =>
        materializer.Find(path)?.Snapshot() ?? new ExecutableStateSnapshot(path, NodeState.Pending);

    /// <summary>各执行节点在快照里的一刻状态：静态文本、执行与次数上限。</summary>
    public IReadOnlyList<ExecutableSnapshot> Executables(IReadOnlyList<GraphExecutionSnapshot> executions)
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

        return
        [
            .. graph.ExecutableNodes.Select(node => new ExecutableSnapshot(
                Executable(node),
                staticPorts.For(node),
                staticPorts.ConstantsFor(node),
                byNode.GetValueOrDefault(node.Path) ?? [],
                materializer.Find(node.Path)?.Progress.Executions ?? 0,
                LimitOf(node.Path) ?? node.MaxExecutions)),
        ];
    }

    /// <summary>各执行节点在快照里的一刻状态。</summary>
    public IReadOnlyList<ExecutableStateSnapshot> ExecutableStates() =>
        [.. graph.ExecutableNodes.Select(node => State(node.Path))];

    /// <summary>各容器在快照里的一刻状态：内容齐备情况、成员活动与发布。</summary>
    public IReadOnlyList<ContainerSnapshot> Containers()
    {
        List<ContainerSnapshot> snapshots = [];
        foreach (ContainerNode container in graph.Containers)
        {
            snapshots.Add(materializer.Find(container.Path) is RuntimeContainer runtime
                ? Of(runtime)
                : new ContainerSnapshot(
                    container.Path,
                    ActivityState(container),
                    ChildPaths(container),
                    staticPorts.ConstantsFor(container),
                    0,
                    LimitOf(container.Path) ?? container.MaxExecutions)
                {
                    Outputs = [.. container.Outputs.Select(Output)],
                });
        }

        return snapshots;
    }

    /// <summary>已实例化容器的一刻状态。成员状态只在已实例化成员上读，未实例化成员视为没有活动。</summary>
    private ContainerSnapshot Of(RuntimeContainer container)
    {
        NodeState state;
        if (container.Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (container.Released)
        {
            state = NodeState.Done;
        }
        else
        {
            state = ActivityState(container.Container);
        }

        return new ContainerSnapshot(
            container.Path,
            state,
            ChildPaths(container.Container),
            staticPorts.ConstantsFor(container.Container),
            container.Progress.Executions,
            LimitOf(container.Path) ?? container.Container.MaxExecutions)
        {
            Outputs = [.. container.Container.Outputs.Select(Output)],
        };
    }

    /// <summary>任务流程视图：步骤与分组来自执行节点与容器的当前状态，名称与说明来自流程模板。</summary>
    public IFlowView View(
        FlowDefinition flow,
        IReadOnlyList<ExecutableSnapshot> executables,
        IReadOnlyList<ExecutableStateSnapshot> states,
        IReadOnlyList<ContainerSnapshot> containers)
    {
        Dictionary<NodePath, NodeState> stateByPath = states.ToDictionary(state => state.Node, state => state.State);

        return new FlowView(
            flow.Name.Value,
            "graph",
            flow.Description,
            [.. graph.StartPorts.Select(Output)],
            [.. executables.Select(executable => Step(
                executable,
                stateByPath.GetValueOrDefault(executable.Executable.Path, NodeState.Pending)))],
            [.. containers.Select(container => new FlowGroupView(
                container.Path.Value,
                container.Path.Label.Value,
                [.. container.Children.Select(child => child.Value)])
            {
                ParentPath = container.Path.Parent?.Value,
                State = container.State.ToString(),
                Outputs = container.Outputs,
                MaxExecutions = container.MaxExecutions,
            })]);
    }

    private static FlowStepView Step(ExecutableSnapshot executable, NodeState state) => new(
        executable.Executable.Path.Value,
        executable.Executable.Label.Value,
        executable.Executable.Kind.Value,
        state.ToString(),
        [.. executable.Executable.Inputs.Select(Input)],
        [.. executable.Executable.Outputs.Select(Output)],
        [.. executable.Executions.Select(Execution)],
        executable.MaxExecutions)
    {
        ParentPath = executable.Executable.Path.Parent?.Value,
    };

    private static FlowExecutableView Executable(ExecutableNode node) => new(
        node.Path,
        node.Label,
        node.Kind,
        node.Type,
        node.Spec,
        node.Inputs,
        node.Outputs,
        node.OutputBindings,
        node.Incoming,
        node.MaxExecutions);

    private static FlowPortView Input(InputPort port) => new(
        port.Name.Value,
        port.Type.Name,
        port.Description ?? port.Default,
        port.Trigger);

    private static FlowPortView Output(Port port) => new(
        port.Name.Value,
        port.Type.Name,
        port.Description ?? port.Value,
        port.Trigger);

    private static FlowExecutionView Execution(GraphExecutionSnapshot execution) => new(
        execution.Id.Value,
        execution.State.ToString(),
        execution.Progress,
        []);

    /// <summary>方案给出的执行或发布次数上限，没有方案时为空。</summary>
    private int? LimitOf(NodePath path) => graph.PlanOf(path)?.MaxExecutions;

    /// <summary>容器的直接子节点路径，先根序。</summary>
    private static IReadOnlyList<NodePath> ChildPaths(ContainerNode container) =>
        [.. container.Children.Select(child => child.Path)];

    /// <summary>容器里已实例化成员的活动状态：有在跑或已产出即运行中，都没有即待办。</summary>
    private NodeState ActivityState(ContainerNode container) =>
        graph.ExecutablesIn(container.Path).Any(MemberActive) ? NodeState.Running : NodeState.Pending;

    /// <summary>成员执行节点是否有在跑的执行或已产出，未实例化的成员没有活动。</summary>
    private bool MemberActive(NodePath executable) =>
        materializer.Find(executable) is RuntimeExecutable node
            && (node.HasActive || node.Progress.Published);
}