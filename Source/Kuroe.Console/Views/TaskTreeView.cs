using Kuroe.Flow.Presentation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm;
using Kuroe.Pending;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>任务节点树：按流程视图列出分组、步骤与执行，执行作为步骤的子行。</summary>
internal sealed class TaskTreeView(Terminal terminal, PendingService pendings)
{
    private readonly Terminal _terminal = terminal;
    private readonly PendingService _pendings = pendings;

    public void Print(TaskSnapshot task)
    {
        Facts facts = new(task, _pendings);
        _terminal.Write(StartLine(task.View.StartPorts));
        _terminal.Write(Build(task, facts));
    }

    private static Paragraph Header() => new ViewLine().Add("节点", Styles.Hint).Build();

    private static Tree Build(TaskSnapshot task, Facts facts)
    {
        List<TreeNodeData> nodes =
        [
            .. task.Containers.Select(container => new TreeNodeData(
                container.Path.Value,
                container.Path.Parent?.Value,
                GroupLine(facts, container),
                [.. Details(facts, container)])),
            .. task.Executables.Select(executable => new TreeNodeData(
                executable.Executable.Path.Value,
                executable.Executable.Path.Parent?.Value,
                StepLine(facts, executable),
                [.. Details(facts, executable)])),
        ];

        Dictionary<string, List<TreeNodeData>> byParent = nodes
            .GroupBy(node => node.ParentPath ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.ToList());
        Tree tree = new(Header()) { Guide = TreeGuide.Line, Style = Styles.Border };
        foreach (TreeNodeData node in byParent.GetValueOrDefault(string.Empty) ?? [])
        {
            Add(tree, node);
        }

        return tree;

        void Add(IHasTreeNodes parent, TreeNodeData node)
        {
            TreeNode branch = parent.AddNode(node.Line);
            foreach (IRenderable detail in node.DetailLines)
            {
                branch.AddNode(detail);
            }

            foreach (TreeNodeData child in byParent.GetValueOrDefault(node.Path) ?? [])
            {
                Add(branch, child);
            }
        }
    }

    private static Paragraph StartLine(IReadOnlyList<FlowPortView> ports)
    {
        ViewLine line = new ViewLine().Add("开始端口", Styles.Key);
        if (ports.Count > 0)
        {
            line.Add(string.Join('、', ports.Select(port => port.Text ?? port.Name)), Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph GroupLine(Facts facts, ContainerSnapshot container)
    {
        NodeState state = container.State;
        (int done, int total) = facts.Progress(container.Path);

        ViewLine line = new ViewLine()
            .Add(container.Path.Label.Value, Styles.Key)
            .Add("容器", Styles.Hint)
            .Add(ViewLabels.Of(state), ViewLabels.StyleOf(state))
            .Add($"成员 {done}/{total} 完成", Styles.Hint);

        if (container.PublicationCount > 0 || container.MaxExecutions != ExecutableNode.DefaultMaxExecutions)
        {
            bool unlimited = container.MaxExecutions == int.MaxValue;
            bool capped = !unlimited && container.PublicationCount >= container.MaxExecutions;
            string publishes = unlimited
                ? $"发布 {container.PublicationCount} 次"
                : $"发布 {container.PublicationCount}/{container.MaxExecutions} 次";
            line.Add(capped ? $"{publishes}，已达上限" : publishes, capped ? Styles.Warning : Styles.Hint);
        }

        if (container.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', container.Outputs.Select(port => port.Text ?? port.Name))}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph StepLine(Facts facts, ExecutableSnapshot step)
    {
        NodePath path = step.Executable.Path;
        NodeState state = facts.State(path);
        ViewLine line = new ViewLine()
            .Add(step.Executable.Label.Value, Styles.Key)
            .Add(step.Executable.Kind.Value, Styles.Hint);

        if (facts.Waiting(path) is { } waiting)
        {
            line.Add(ViewLabels.WaitingLabel(waiting), Styles.Warning);
        }
        else
        {
            line.Add(ViewLabels.Of(state), ViewLabels.StyleOf(state));
        }

        if (facts.Block(path) is { } block)
        {
            line.Add(
                $"阻塞原因 {ViewLabels.OneLine(block.Summary, ViewLabels.BlockLimit)}",
                Styles.Warning);
        }

        if (facts.CanRestore(path))
        {
            line.Add("可恢复", Styles.Success);
        }

        if (facts.StaticText(path, LlmNodeTypes.ModelPort) is { } model)
        {
            line.Add($"模型 {model}", Styles.Hint);
        }

        if (step.Executable.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', step.Executable.Outputs.Select(port => port.Label()))}", Styles.Hint);
        }

        if (step.Executable.Type.Shape.DispatchesExecutions)
        {
            ExecutableSnapshot? snapshot = facts.Executable(path);
            int count = snapshot?.ExecutionCount ?? facts.Executions(path).Count;
            int limit = snapshot?.MaxExecutions ?? step.MaxExecutions;
            bool configured = limit != ExecutableNode.DefaultMaxExecutions;
            bool capped = count >= limit;
            if (count > 0 || configured)
            {
                string executions = configured ? $"执行 {count}/{limit} 次" : $"执行 {count} 次";
                line.Add(executions, capped ? Styles.Warning : Styles.Hint);
            }

            if (capped)
            {
                line.Add("已达上限", Styles.Warning);
            }
        }

        return line.Build();
    }
private static IEnumerable<IRenderable> Details(Facts facts, ContainerSnapshot group)
    {
        NodePath path = group.Path;

        foreach (string name in group.Outputs.Select(port => port.Name))
        {
            if (facts.ContainerConstantText(path, new PortName(name)) is { Length: > 0 } text)
            {
                yield return new ViewLine().Add($"{name}={text}", Styles.Hint).Build();
            }
        }
    }

    private static IEnumerable<IRenderable> Details(Facts facts, ExecutableSnapshot step)
    {
        NodePath path = step.Executable.Path;

        foreach (PortName name in step.Executable.Outputs
            .Where(port => port.IsConstant)
            .Select(port => port.Name))
        {
            if (facts.ConstantText(path, name) is { Length: > 0 } text)
            {
                yield return new ViewLine().Add($"{name.Value}={text}", Styles.Hint).Build();
            }
        }

        foreach (GraphExecutionSnapshot execution in facts.Executions(path).OrderBy(execution => execution.Id.Value))
        {
            yield return ExecutionLine(execution);
        }
    }

    private static Paragraph ExecutionLine(GraphExecutionSnapshot execution)
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

    private sealed record TreeNodeData(
        string Path,
        string? ParentPath,
        Paragraph Line,
        IReadOnlyList<IRenderable> DetailLines);
/// <summary>一棵任务树里各节点的一刻读数，渲染时一次建好。</summary>
    private sealed class Facts
    {
        private readonly Dictionary<NodePath, NodeState> _states;
        private readonly Dictionary<NodePath, NodeBlock> _blocks;
        private readonly HashSet<NodePath> _canRestore;
        private readonly Dictionary<NodePath, ExecutableSnapshot> _snapshots;
        private readonly Dictionary<NodePath, IReadOnlyList<GraphExecutionSnapshot>> _executions;
        private readonly Dictionary<NodePath, ContainerSnapshot> _containers;
        private readonly Dictionary<NodePath, PendingNode> _waiting;
        private readonly Dictionary<NodePath, IPortTexts> _staticTexts;
        private readonly Dictionary<NodePath, IPortTexts> _constantTexts;

        public Facts(TaskSnapshot task, PendingService pendings)
        {
            _states = task.ExecutableStates.ToDictionary(state => state.Node, state => state.State);
            _blocks = [];
            foreach (ExecutableStateSnapshot state in task.ExecutableStates)
            {
                if (state.Block is { } block)
                {
                    _blocks[state.Node] = block;
                }
            }

            _canRestore = [.. task.ExecutableStates.Where(state => state.CanRestore).Select(state => state.Node)];
            _snapshots = task.Executables.ToDictionary(node => node.Executable.Path);
            _executions = task.Executables.ToDictionary(node => node.Executable.Path, node => node.Executions);
            _containers = task.Containers.ToDictionary(container => container.Path);
            _staticTexts = task.Executables.ToDictionary(node => node.Executable.Path, node => node.StaticTexts);
            _constantTexts = task.Executables.ToDictionary(node => node.Executable.Path, node => node.ConstantTexts);
            _waiting = pendings.Pending(task.Id) is { IsError: false } found
                ? found.Value.ToDictionary(node => node.Node)
                : [];
        }

        public NodeState State(NodePath? path) =>
            path is { } node && _states.TryGetValue(node, out NodeState state) ? state : NodeState.Pending;

        public NodeBlock? Block(NodePath? path) =>
            path is { } node ? _blocks.GetValueOrDefault(node) : null;

        public bool CanRestore(NodePath? path) =>
            path is { } node && _canRestore.Contains(node);

        public PendingNode? Waiting(NodePath? path) =>
            path is { } node && _waiting.TryGetValue(node, out PendingNode pending) ? pending : null;

        public IReadOnlyList<GraphExecutionSnapshot> Executions(NodePath? path) =>
            path is { } node && _executions.TryGetValue(node, out IReadOnlyList<GraphExecutionSnapshot>? values)
                ? values
                : [];

        public ExecutableSnapshot? Executable(NodePath? path) =>
            path is { } node ? _snapshots.GetValueOrDefault(node) : null;

        public ContainerSnapshot? Container(NodePath? path) =>
            path is { } node && _containers.TryGetValue(node, out ContainerSnapshot? container) ? container : null;

        public string? StaticText(NodePath? node, PortName port) =>
            node is { } path && _staticTexts.TryGetValue(path, out IPortTexts? texts) ? texts.Text(port) : null;

        public string? ConstantText(NodePath? node, PortName port) =>
            node is { } path && _constantTexts.TryGetValue(path, out IPortTexts? texts) ? texts.Text(port) : null;

        public string? ContainerConstantText(NodePath? container, PortName port) =>
            Container(container)?.ConstantTexts.Text(port);

        public (int Done, int Total) Progress(NodePath? container)
        {
            if (container is not { } path || !_containers.TryGetValue(path, out ContainerSnapshot? snapshot))
            {
                return (0, 0);
            }

            IReadOnlyList<NodePath> members = [.. snapshot.Children.SelectMany(Expand)];

            return (members.Count(member => State(member) == NodeState.Done), members.Count);

            IEnumerable<NodePath> Expand(NodePath child) =>
                _containers.TryGetValue(child, out ContainerSnapshot? nested)
                    ? nested.Children.SelectMany(Expand)
                    : [child];
        }
    }
}