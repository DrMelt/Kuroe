using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Presentation.Shared;
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
            .. task.View.Groups.Select(group => new TreeNodeData(
                group.Path,
                group.ParentGroupPath,
                GroupLine(facts, group),
                [.. Details(group)])),
            .. task.View.Steps.Select(step => new TreeNodeData(
                step.Path,
                step.GroupPath,
                StepLine(facts, step),
                [.. Details(step)])),
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

    private static Paragraph GroupLine(Facts facts, FlowGroupView group)
    {
        (int done, int total) = facts.Progress(group.Path);

        ViewLine line = new ViewLine()
            .Add(group.Label, Styles.Key)
            .Add("容器", Styles.Hint)
            .Add(ViewLabels.Of(group.State), ViewLabels.StyleOf(group.State))
            .Add($"成员 {done}/{total} 完成", Styles.Hint);

        if (group.PublicationCount > 0 || group.MaxExecutions != int.MaxValue)
        {
            bool unlimited = group.MaxExecutions == int.MaxValue;
            bool capped = !unlimited && group.PublicationCount >= group.MaxExecutions;
            string publishes = unlimited
                ? $"发布 {group.PublicationCount} 次"
                : $"发布 {group.PublicationCount}/{group.MaxExecutions} 次";
            line.Add(capped ? $"{publishes}，已达上限" : publishes, capped ? Styles.Warning : Styles.Hint);
        }

        if (group.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', group.Outputs.Select(port => port.Text ?? port.Name))}", Styles.Hint);
        }

        return line.Build();
    }

    private static Paragraph StepLine(Facts facts, FlowStepView step)
    {
        ViewLine line = new ViewLine()
            .Add(step.Label, Styles.Key)
            .Add(step.Kind, Styles.Hint);

        if (facts.Waiting(step.Path) is { } waiting)
        {
            line.Add(ViewLabels.WaitingLabel(waiting), Styles.Warning);
        }
        else
        {
            line.Add(ViewLabels.Of(step.State), ViewLabels.StyleOf(step.State));
        }

        if (step.Block is { } block)
        {
            line.Add($"阻塞原因 {ViewLabels.OneLine(block, ViewLabels.BlockLimit)}", Styles.Warning);
        }

        if (step.CanRestore)
        {
            line.Add("可恢复", Styles.Success);
        }

        if (step.Inputs.FirstOrDefault(port => port.Name == LlmNodeTypes.ModelPort.Value) is { Text: { } model })
        {
            line.Add($"模型 {model}", Styles.Hint);
        }

        if (step.Outputs.Count > 0)
        {
            line.Add($"输出 {string.Join('、', step.Outputs.Select(port => port.Name))}", Styles.Hint);
        }

        int count = step.ExecutionCount;
        int limit = step.MaxExecutions;
        bool configured = limit != FlowStepView.DefaultMaxExecutions;
        bool capped = count >= limit;
        if (count > 0 || configured)
        {
            string executions = configured ? $"执行 {count}/{limit} 次" : $"执行 {count} 次";
            line.Add(executions, capped ? Styles.Warning : Styles.Hint);
        }

        if (capped && count > 0)
        {
            line.Add("已达上限", Styles.Warning);
        }

        return line.Build();
    }
    private static IEnumerable<IRenderable> Details(FlowGroupView group)
    {
        foreach (FlowPortView port in group.Outputs)
        {
            if (port.Constant && port.Text is { Length: > 0 } text)
            {
                yield return new ViewLine().Add($"{port.Name}={text}", Styles.Hint).Build();
            }
        }
    }

    private static IEnumerable<IRenderable> Details(FlowStepView step)
    {
        foreach (FlowPortView port in step.Outputs.Where(port => port.Constant))
        {
            if (port.Text is { Length: > 0 } text)
            {
                yield return new ViewLine().Add($"{port.Name}={text}", Styles.Hint).Build();
            }
        }

        foreach (FlowExecutionView execution in step.Executions.OrderBy(execution => execution.Id))
        {
            yield return ExecutionLine(execution);
        }
    }

    private static Paragraph ExecutionLine(FlowExecutionView execution)
    {
        (string id, string round, string state, string elapsed, string? failures) =
            ViewLabels.ExecutionParts(execution);
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
        private readonly Dictionary<string, FlowStepView> _steps;
        private readonly Dictionary<string, FlowGroupView> _groups;
        private readonly Dictionary<string, PendingNode> _waiting;

        public Facts(TaskSnapshot task, PendingService pendings)
        {
            _steps = task.View.Steps.ToDictionary(step => step.Path);
            _groups = task.View.Groups.ToDictionary(group => group.Path);
            _waiting = pendings.Pending(task.Id) is { IsError: false } found
                ? found.Value.ToDictionary(node => node.Step.Value)
                : [];
        }

        public PendingNode? Waiting(string path) =>
            _waiting.TryGetValue(path, out PendingNode pending) ? pending : null;

        public (int Done, int Total) Progress(string container)
        {
            if (!_groups.TryGetValue(container, out FlowGroupView? group))
            {
                return (0, 0);
            }

            IReadOnlyList<string> members = [.. group.Members.SelectMany(Expand)];

            return (members.Count(member => State(member) == FlowItemState.Done), members.Count);

            IEnumerable<string> Expand(string child) =>
                _groups.TryGetValue(child, out FlowGroupView? nested)
                    ? nested.Members.SelectMany(Expand)
                    : [child];
        }

        private FlowItemState State(string path)
        {
            if (_steps.TryGetValue(path, out FlowStepView? step))
            {
                return step.State;
            }

            return _groups.TryGetValue(path, out FlowGroupView? group)
                ? group.State
                : FlowItemState.Pending;
        }
    }
}