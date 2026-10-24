using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;

namespace Kuroe.Console.Views;

/// <summary>任务列表 → 任务概览与执行 → 执行详情的三级下钻。进入时独占终端，退出时冲刷排队的通知。</summary>
internal sealed class TaskBrowser(
    TaskRegistry registry,
    TaskService tasks,
    PendingService pendings,
    FlowService flows,
    TaskListView list,
    TaskOverviewView overview,
    TaskTreeView tree,
    ExecutionDetailView executionView,
    Terminal terminal,
    ResultView results)
{
    /// <summary>一个菜单项：进下一级、执行动作或返回。</summary>
    private sealed record Item(
        string Label,
        string Action,
        TaskId? Task = null,
        ExecutionId? Execution = null,
        NodePath? Node = null);

    private static readonly Item Back = new("← 返回上一级", "back");
    private static readonly Item Exit = new("× 退出浏览", "exit");
    private static readonly Item Refresh = new("~ 刷新", "refresh");
    private static readonly Item SubmitItem = new("+ 提交任务", "submit");
    private static readonly Item ClearItem = new("- 清理已完成与已取消的任务", "clear");

    public void Browse()
    {
        using (terminal.Exclusive())
        {
            PickTask();
        }
    }

    /// <summary>按流程与开始输入提交任务。</summary>
    public void Submit(FlowName flow, StartInputs inputs)
    {
        ErrorOr<TaskSnapshot> submitted = tasks.Submit(inputs, flow, null);
        if (submitted.IsError)
        {
            results.Reject(submitted.ErrorsOrEmptyList);
            return;
        }

        TaskSnapshot task = submitted.Value;
        terminal.Ok($"已提交 {task.Id}（流程 {task.Flow.Name}），{task.LiveExecutions} 个执行已启动。");
        list.Print([task]);
    }

    /// <summary>丢掉已完成或已取消的内存任务。</summary>
    public void Clear() => terminal.Ok($"已丢掉 {registry.ClearSettled()} 个任务。");

    /// <summary>改任务标题。</summary>
    public void Rename(TaskId id, string title) => results.Report(tasks.Rename(id, title), "已改标题。");

    /// <summary>批准全部待批准的节点。</summary>
    public void Approve(TaskId id) => results.Report(pendings.Approve(id), $"{id} 已批准，继续下一步。");

    /// <summary>回答等待输入的节点，nodePath 为空时回答唯一待输入节点。</summary>
    public void Answer(TaskId id, string? nodePath, string input) =>
        results.Report(pendings.Answer(id, nodePath, input), $"{id} 已收到回答，继续下一步。");

    /// <summary>返工被阻塞的节点。</summary>
    public void Rework(TaskId id) => results.Report(tasks.Rework(id), $"{id} 已返工。");

    /// <summary>取消任务。</summary>
    public void StopTask(TaskId id) => results.Report(tasks.StopTask(id), $"{id} 已取消。");

    /// <summary>取消单个执行。</summary>
    public void StopExecution(ExecutionId id) => results.Report(tasks.StopExecution(id), $"{id} 已取消。");

    /// <summary>打开任务节点树页。</summary>
    public void ShowTree(TaskId id) => ViewPage.Browse(terminal, "节点树", () => PrintTree(id));

    /// <summary>打开任务过程记录页。</summary>
    public void ShowJournal(TaskId id) => ViewPage.Browse(terminal, "过程记录", () => PrintJournal(id));

    private void PrintTree(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        TaskSnapshot task = found.Value.Snapshot();
        terminal.Page("主菜单", "任务", $"{task.Id} {task.Title}", "节点树");
        tree.Print(task);
    }

    private void PrintJournal(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        TaskSnapshot task = found.Value.Snapshot();
        terminal.Page("主菜单", "任务", $"{task.Id} {task.Title}", "过程记录");
        terminal.Write(Blocks.Section(BoxKind.View, "过程记录", JournalView.Build(task.Journal, task.DroppedJournal)));
    }

    private void Submit()
    {
        IReadOnlyList<FlowDefinition> all = flows.All();
        if (all.Count == 0)
        {
            terminal.Hint("流程库为空，先在「流程」菜单导入流程。");
            return;
        }

        if (Choices.Pick(terminal, "选择流程", [.. all.Select(flow => flow.Name.Value)]) is not { } index)
        {
            return;
        }

        if (AskInputs(all[index]) is { } inputs)
        {
            Submit(all[index].Name, inputs);
        }
    }

    /// <summary>按开始端口询问不带固定值的项目，每次重印全部项目，任一回答取消即放弃提交，最后确认整个输入。</summary>
    private StartInputs? AskInputs(FlowDefinition flow)
    {
        List<Port> ports = [.. flow.StartOutputs.Where(port => port.Value is null)];
        List<FormInfo> info =
        [
            new("流程", flow.Name.Value),
            .. flow.StartOutputs
                .Where(port => port.Value is not null)
                .Select(port => new FormInfo(port.Name.Value, port.Value!)),
        ];
        InputForm form = new(
            terminal,
            "开始输入",
            [.. ports.Select(port => new FormField(port.Name.Value, port.Description))],
            info);

        if (form.Run() is not { } values)
        {
            return null;
        }

        if (!Confirmation.Ask(terminal, "提交这个任务？", "提交任务", "取消提交"))
        {
            return null;
        }

        return new StartInputs(
            [.. ports.Select((port, index) => new StartInput(port.Name, values[index]))]);
    }

    private void PromptRename(TaskId id, string currentTitle)
    {
        InputForm form = new(
            terminal,
            "改任务标题",
            [new FormField("新标题")],
            [new FormInfo("当前标题", currentTitle)]);
        if (form.Run() is { } values)
        {
            Rename(id, values[0]);
        }
    }

    private void PickTask()
    {
        while (true)
        {
            IReadOnlyList<TaskSnapshot> snapshots = registry.Snapshots();
            terminal.Page("主菜单", "任务");
            list.Print(snapshots);
            terminal.NewLine();

            Item picked = Choose("选择任务",
                [.. snapshots.Select(task => new Item(TaskLabel(task), "task", task.Id)), SubmitItem, ClearItem, Refresh, Exit]);
            switch (picked.Action)
            {
                case "exit" or "back":
                    return;

                case "submit":
                    Submit();
                    break;

                case "clear":
                    Clear();
                    break;
            }

            if (picked.Task is { } id)
            {
                PickExecution(id);
            }
        }
    }

    private void PickExecution(TaskId id)
    {
        while (true)
        {
            if (registry.Find(id) is not { IsError: false } found)
            {
                return;
            }

            TaskSnapshot task = found.Value.Snapshot();

            terminal.Page("主菜单", "任务", $"{task.Id} {task.Title}");
            overview.Print(task);
            terminal.NewLine();

            List<Item> items = [.. task.Executables.SelectMany(node => node.Executions)
                .OrderBy(execution => execution.Id.Value)
                .Select(execution => new Item(ExecutionItemLabel(execution), "execution", Task: id, Execution: execution.Id))];
            AddTaskActions(items, task);
            items.Add(new Item("* 改任务标题", "rename", Task: id));
            items.Add(new Item("≡ 查看节点树", "tree", Task: id));
            items.Add(new Item("≡ 查看过程记录", "journal", Task: id));
            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("任务内", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "execution" when picked.Execution is { } executionId:
                    BrowseExecution(executionId);
                    break;

                case "tree":
                    ShowTree(id);
                    break;

                case "rename":
                    PromptRename(id, task.Title);
                    break;

                case "journal":
                    ShowJournal(id);
                    break;

                case "approve":
                    Approve(id);
                    break;

                case "answer":
                    PromptAnswer(id);
                    break;

                case "rework":
                    Rework(id);
                    break;

                case "stopTask":
                    ConfirmStopTask(id);
                    break;

                case "stopExecution" when picked.Execution is { } target:
                    StopExecution(target);
                    break;
            }
        }
    }

    private void PromptAnswer(TaskId id)
    {
        ErrorOr<List<PendingNode>> pending = pendings.Pending(id);
        if (pending.IsError)
        {
            return;
        }

        List<PendingNode> waiting = [.. pending.Value.Where(node => node.Kind == PendingKind.Answer)];
        if (waiting.Count == 0)
        {
            terminal.Hint("该任务没有等待输入的节点。");
            return;
        }

        if (registry.Find(id) is not { IsError: false } found)
        {
            return;
        }

        TaskSnapshot task = found.Value.Snapshot();
        List<Item> items = [.. waiting.Select(node => new Item(
            AnswerItemLabel(task, node), "answer", Node: node.Node))];

        if (Choose("选择要回答的节点", items).Node is not { } nodePath)
        {
            return;
        }

        ExecutableNode? executable = task.Graph[nodePath] as ExecutableNode;
        string? question = NodeParameterView.Question(executable?.Type);
        List<FormInfo> info = [new("节点", nodePath.Value)];
        if (question is { Length: > 0 })
        {
            info.Add(new("提问", question));
        }

        InputForm form = new(terminal, "回答等待输入", [new FormField("回答")], info);
        if (form.Run() is { } values)
        {
            Answer(id, nodePath.Value, values[0]);
        }
    }

    private static string AnswerItemLabel(TaskSnapshot task, PendingNode node)
    {
        ExecutableNode? executable = task.Graph[node.Node] as ExecutableNode;

        return $"{node.Node.Value} · {ViewLabels.WaitingLabel(node, executable?.Type)}";
    }

    /// <summary>打印执行详情。</summary>
    public void ShowExecution(ExecutionId executionId)
    {
        ErrorOr<(TaskSnapshot Task, ExecutionSnapshot Execution)> found = FindExecution(executionId);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        terminal.Page(
            "主菜单",
            "任务",
            $"{found.Value.Task.Id} {found.Value.Task.Title}",
            $"{found.Value.Execution.Id} {found.Value.Execution.Input.Node}");
        executionView.Print(found.Value.Task, found.Value.Execution);
    }

    private void BrowseExecution(ExecutionId executionId)
    {
        while (true)
        {
            if (FindExecution(executionId) is not { IsError: false } found)
            {
                return;
            }

            TaskSnapshot task = found.Value.Task;
            ExecutionSnapshot snapshot = found.Value.Execution;

            terminal.Page("主菜单", "任务", $"{task.Id} {task.Title}", $"{snapshot.Id} {snapshot.Input.Node}");
            executionView.Print(task, snapshot);
            terminal.NewLine();

            List<Item> items = [.. snapshot.Input.Inputs
                .Where(value => value.Origin.FromExecution is not null)
                .Select(value => new Item($"↑ {value.Origin.Label}", "execution", Execution: value.Origin.FromExecution))
                .DistinctBy(item => item.Execution)];

            if (!snapshot.State.IsSettled())
            {
                items.Add(new Item("× 取消该执行", "stopExecution", Execution: executionId));
            }

            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("执行详情", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "execution" when picked.Execution is { } upstream:
                    executionId = upstream;
                    break;

                case "stopExecution":
                    StopExecution(executionId);
                    break;
            }
        }
    }

    /// <summary>按执行号取执行与其所属任务的快照。</summary>
    private ErrorOr<(TaskSnapshot Task, ExecutionSnapshot Execution)> FindExecution(ExecutionId id)
    {
        ErrorOr<Execution> execution = registry.FindExecution(id);
        if (execution.IsError)
        {
            return execution.ErrorsOrEmptyList;
        }

        ExecutionSnapshot snapshot = execution.Value.Snapshot();
        ErrorOr<WorkTask> owner = registry.Find(snapshot.Input.Task);

        return owner.IsError
            ? owner.ErrorsOrEmptyList
            : (owner.Value.Snapshot(), snapshot);
    }

    /// <summary>取消整个任务不可恢复，选择后要求再次确认，默认停在保留任务的选项。</summary>
    private void ConfirmStopTask(TaskId id)
    {
        if (Confirmation.Ask(terminal, $"取消{id}？", "× 确认取消整个任务", "← 返回，保留任务"))
        {
            StopTask(id);
        }
    }

    private void AddTaskActions(List<Item> items, TaskSnapshot task)
    {
        if (task.State is TaskState.Done or TaskState.Canceled)
        {
            return;
        }

        // 等待与阻塞可以并存，任务级状态只表达阻塞，入口按节点的实际停留各自给出
        List<PendingNode> waiting = pendings.Pending(task.Id) is { IsError: false } found ? found.Value : [];
        bool blocked = task.ExecutableStates.Any(state => state.State == NodeState.Blocked);

        if (waiting.Any(node => node.Kind == PendingKind.Approve))
        {
            items.Add(new Item("√ 批准待批准的节点，开下一步", "approve", Task: task.Id));
        }

        if (waiting.Any(node => node.Kind == PendingKind.Answer))
        {
            items.Add(new Item("* 回答等待输入的节点", "answer", Task: task.Id));
        }

        if (blocked)
        {
            items.Add(new Item("* 返工被阻塞的节点", "rework", Task: task.Id));
        }

        items.Add(new Item("× 取消整个任务", "stopTask", Task: task.Id));
    }

    private Item Choose(string title, IReadOnlyList<Item> items) =>
        terminal.Prompt(new MenuPrompt<Item>(title, items, item => item.Label, Back));

    private static string ExecutionItemLabel(ExecutionSnapshot execution) =>
        $"{execution.Input.Node} · {ViewLabels.ExecutionLabel(execution)}";

    private static string TaskLabel(TaskSnapshot task) =>
        $"#{task.Id.Value} {task.Title} · {ViewLabels.Of(task.State)}";
}

