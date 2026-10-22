using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Pending;
using Kuroe.Presentation;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>任务节点树：按流程树列出容器、执行节点与执行，执行作为执行节点的子行。</summary>
internal sealed class TaskTreeView(Terminal terminal, PendingService pendings)
{
    private readonly Terminal _terminal = terminal;
    private readonly PendingService _pendings = pendings;

    public void Print(TaskSnapshot task)
    {
        Facts facts = new(task, _pendings);
        _terminal.Write(NodeTree.Build(task.Graph, Header(), node => Line(facts, node), node => Details(facts, node)));
    }

    private static Paragraph Header() => new ViewLine().Add("节点", Styles.Hint).Build();

    private static Paragraph Line(Facts facts, GraphNode node) => node switch
    {
        StartNode start => StartLine(start),
        ContainerNode container => ContainerLine(facts, container),
        ExecutableNode executable => ExecutableLine(facts, executable),
        _ => new ViewLine().Add(node.Label.Value, Styles.Key).Build(),
    };

    private static Paragraph StartLine(StartNode start)
    {
        ViewLine line = new ViewLine().Add("Start", Styles.Key).Add("开始", Styles.Hint);
        if (start.Outputs.Count > 0)
        {
            line.Add($"端口 {string.Join('、', start.Outputs.Select(port => port.StartLabel()))}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph ContainerLine(Facts facts, ContainerNode container)
    {
        ContainerSnapshot? snapshot = facts.Container(container.Path);
        NodeState state = snapshot?.State ?? NodeState.Pending;
        (int done, int total) = facts.Progress(container.Path);

        ViewLine line = new ViewLine()
            .Add(container.Label.Value, Styles.Key)
            .Add("容器", Styles.Hint)
            .Add(ViewLabels.Of(state), ViewLabels.StyleOf(state))
            .Add($"成员 {done}/{total} 完成", Styles.Hint);

        if (container.PortBindings is { Count: > 0 } bindings)
        {
            line.Add($"端口 {string.Join('、', bindings.Keys.Select(port => port.Value))}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph ExecutableLine(Facts facts, ExecutableNode executable)
    {
        NodeState state = facts.State(executable.Path);
        ViewLine line = new ViewLine()
            .Add(executable.Label.Value, Styles.Key)
            .Add(executable.Kind.Value, Styles.Hint);

        if (facts.Waiting(executable.Path) is { } waiting)
        {
            line.Add(ViewLabels.WaitingLabel(waiting, executable.Type), Styles.Warning);
        }
        else
        {
            line.Add(ViewLabels.Of(state), ViewLabels.StyleOf(state));
        }

        if (executable.Model is { } model)
        {
            line.Add($"模型 {ViewLabels.ModelLabel(model)}", Styles.Hint);
        }

        if (executable.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', executable.Outputs.Select(port => port.Name.Value))}", Styles.Hint);
        }

        if (executable.Type.Shape.DispatchesExecutions)
        {
            int count = facts.Executions(executable.Path).Count;
            bool configured = executable.MaxExecutions != ExecutableNode.DefaultMaxExecutions;
            bool capped = count >= executable.MaxExecutions;
            if (count > 0 || configured)
            {
                string executions = configured ? $"执行 {count}/{executable.MaxExecutions} 次" : $"执行 {count} 次";
                line.Add(executions, capped ? Styles.Warning : Styles.Hint);
            }

            if (capped)
            {
                line.Add("已达上限", Styles.Warning);
            }
        }

        return line.Build();
    }

    private static IEnumerable<IRenderable> Details(Facts facts, GraphNode node)
    {
        if (node is not ExecutableNode executable)
        {
            yield break;
        }

        foreach (ExecutionSnapshot execution in facts.Executions(executable.Path).OrderBy(execution => execution.Id.Value))
        {
            yield return ExecutionLine(execution);
        }
    }

    private static Paragraph ExecutionLine(ExecutionSnapshot execution)
    {
        (string id, string round, string state, string elapsed, string? failures) = ViewLabels.ExecutionParts(execution);
        ViewLine line = new ViewLine()
            .Add(id, Styles.Key)
            .Add(round, Styles.Hint)
            .Add(state, ViewLabels.StyleOf(execution.State))
            .Add(elapsed, Styles.Hint);

        if (failures is not null)
        {
            line.Add(failures, Styles.Error);
        }

        return line.Build();
    }

    /// <summary>一棵任务树里各节点的一刻读数，渲染时一次建好。</summary>
    private sealed class Facts
    {
        private readonly Dictionary<NodePath, NodeState> _states;
        private readonly Dictionary<NodePath, IReadOnlyList<ExecutionSnapshot>> _executions;
        private readonly Dictionary<NodePath, ContainerSnapshot> _containers;
        private readonly Dictionary<NodePath, PendingNode> _waiting;
        private readonly TaskSnapshot _task;

        public Facts(TaskSnapshot task, PendingService pendings)
        {
            _task = task;
            _states = task.ExecutableStates.ToDictionary(state => state.Node, state => state.State);
            _executions = task.Executables.ToDictionary(node => node.Executable.Path, node => node.Executions);
            _containers = task.Containers.ToDictionary(container => container.Path);
            _waiting = pendings.Pending(task.Id) is { IsError: false } found
                ? found.Value.ToDictionary(node => node.Node)
                : [];
        }

        public NodeState State(NodePath path) =>
            _states.TryGetValue(path, out NodeState state) ? state : NodeState.Pending;

        public PendingNode? Waiting(NodePath path) =>
            _waiting.TryGetValue(path, out PendingNode node) ? node : null;

        public IReadOnlyList<ExecutionSnapshot> Executions(NodePath path) =>
            _executions.TryGetValue(path, out IReadOnlyList<ExecutionSnapshot>? executions) ? executions : [];

        public ContainerSnapshot? Container(NodePath path) =>
            _containers.TryGetValue(path, out ContainerSnapshot? container) ? container : null;

        public (int Done, int Total) Progress(NodePath container)
        {
            IReadOnlyList<NodePath> members = _task.Graph.ExecutablesIn(container);

            return (members.Count(member => State(member) == NodeState.Done), members.Count);
        }
    }
}
