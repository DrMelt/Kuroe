using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;

namespace Kuroe.Console.Views;

/// <summary>任务列表 → 任务概览与执行 → 执行详情的三级下钻。进入时独占终端，退出时冲刷排队的通知。</summary>
internal sealed class TaskBrowser(
    TaskRegistry registry,
    ITaskControl tasks,
    PendingService pendings,
    FlowService flows,
    IHostStartInputs hostStartInputs,
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
        int? Execution = null,
        NodePath? Node = null);

    private static readonly Item Back = new("← 返回上一级", "back");
    private static readonly Item Exit = new("× 退出浏览", "exit");
    private static readonly Item Refresh = new("~ 刷新", "refresh");
    private static readonly Item SubmitItem = new("+ 提交任务", "submit");
    private static readonly Item ClearItem = new("- 清理已完成与已终止的任务", "clear");

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
        terminal.Ok($"已提交 {task.Id}（流程 {task.View.Name}），{task.LiveExecutions} 个执行已启动。");
        list.Print([task]);
    }

    /// <summary>丢掉已完成或已终止的内存任务，同时删除存档。</summary>
    public void Clear()
    {
        int count = tasks.ClearSettled().Count;
        terminal.Ok($"已丢掉 {count} 个任务。");
    }

    /// <summary>改任务标题。</summary>
    public void Rename(TaskId id, string title) => results.Report(tasks.Rename(id, title), "已改标题。");

    /// <summary>批准全部待批准的节点。</summary>
    public void Approve(TaskId id) => results.Report(pendings.Approve(id), $"{id} 已批准，继续下一步。");

    /// <summary>回答等待输入的节点，nodePath 为空时回答唯一待输入节点。</summary>
    public void Answer(TaskId id, string? nodePath, string input) =>
        results.Report(pendings.Answer(id, nodePath, input), $"{id} 已收到回答，继续下一步。");

    /// <summary>重新执行被阻塞的节点。</summary>
    public void Rerun(TaskId id) => results.Report(tasks.Rerun(id), $"{id} 已重新执行。");

    /// <summary>暂停任务。</summary>
    public void Pause(TaskId id) => results.Report(tasks.Pause(id), $"{id} 已暂停。");

    /// <summary>恢复暂停的任务。</summary>
    public void Resume(TaskId id) => results.Report(tasks.Resume(id), $"{id} 已恢复。");

    /// <summary>终止任务。</summary>
    public void StopTask(TaskId id) => results.Report(tasks.StopTask(id), $"{id} 已终止。");

    /// <summary>停止单个执行。</summary>
    public void StopExecution(int id) => results.Report(
        tasks.StopExecution(new Kuroe.Flow.Execution.ExecutionId(id)),
        $"执行 #{id} 已停止。");

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

    /// <summary>按开始端口询问不带固定值且不由宿主给值的项目，默认值作为初始内容预填。
    /// 每次重印全部项目，任一回答取消即放弃提交，最后确认整个输入。</summary>
    private StartInputs? AskInputs(FlowDefinition flow)
    {
        IReadOnlyDictionary<PortName, string> hostInputs = hostStartInputs.Bindings(flow.Name);
        StartInputKind KindOf(Port port) => StartInputRules.Kind(port, hostInputs.ContainsKey(port.Name));

        List<Port> ports = [.. flow.StartPorts.Where(port => KindOf(port) == StartInputKind.Input)];
        List<FormInfo> info =
        [
            new("流程", flow.Name.Value),
            .. flow.StartPorts
                .Where(port => KindOf(port) == StartInputKind.Fixed)
                .Select(port => new FormInfo(port.Name.Value, port.Value ?? string.Empty)),
            .. flow.StartPorts
                .Where(port => KindOf(port) == StartInputKind.Host)
                .Select(port => new FormInfo(port.Name.Value, $"宿主 {hostInputs[port.Name]}")),
            .. flow.StartPorts
                .Where(port => KindOf(port) == StartInputKind.Automatic)
                .Select(port => new FormInfo(port.Name.Value, "自动放行")),
        ];
        InputForm form = new(
            terminal,
            "开始输入",
            [.. ports.Select(port => new FormField(port.Name.Value, port.Description, FieldKind.Multiline, port.Default))],
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
                [Refresh, .. snapshots.Select(task => new Item(TaskLabel(task), "task", task.Id)), SubmitItem, ClearItem, Exit]);
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

            List<Item> items = [Refresh, .. task.Executables.SelectMany(node => node.Executions)
                .OrderBy(execution => execution.Id.Value)
                .Select(execution => new Item(ExecutionItemLabel(execution), "execution", Task: id, Execution: execution.Id.Value))];
            AddTaskActions(items, task);
            items.Add(new Item("* 改任务标题", "rename", Task: id));
            items.Add(new Item("≡ 查看节点树", "tree", Task: id));
            items.Add(new Item("≡ 查看过程记录", "journal", Task: id));
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

                case "rerun":
                    Rerun(id);
                    break;

                case "pauseTask":
                    Pause(id);
                    break;

                case "resumeTask":
                    Resume(id);
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

        List<Item> items = [.. waiting.Select(node => new Item(
            AnswerItemLabel(node), "answer", Node: node.Node))];

        if (Choose("选择要回答的节点", items).Node is not { } nodePath)
        {
            return;
        }

        PendingNode selected = waiting.First(node => node.Node.Value == nodePath.Value);
        List<FormInfo> info = [new("节点", nodePath.Value)];
        if (selected.Question is { Length: > 0 } question)
        {
            info.Add(new("提问", question));
        }

        InputForm form = new(terminal, "回答等待输入", [new FormField("回答", Kind: FieldKind.Multiline)], info);
        if (form.Run() is { } values)
        {
            Answer(id, nodePath.Value, values[0]);
        }
    }

    private static string AnswerItemLabel(PendingNode node) =>
        $"{node.Node.Value} · {ViewLabels.WaitingLabel(node)}";

    /// <summary>打印执行详情。</summary>
    public void ShowExecution(int executionId)
    {
        ErrorOr<(TaskSnapshot Task, Kuroe.Flow.Execution.ExecutionSnapshot Execution)> found = FindExecution(executionId);
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        terminal.Page(
            "主菜单",
            "任务",
            $"{found.Value.Task.Id} {found.Value.Task.Title}",
            $"{found.Value.Execution.Id} {found.Value.Execution.Request.Target}");
        executionView.Print(found.Value.Task, found.Value.Execution);
        terminal.Hint($"› {found.Value.Execution.Id} · 第 {found.Value.Execution.Request.Attempt} 轮"
            + $" · {found.Value.Execution.State}");
    }

    private void BrowseExecution(int executionId)
    {
        while (true)
        {
            if (FindExecution(executionId) is not { IsError: false } found)
            {
                return;
            }

            TaskSnapshot task = found.Value.Task;
            Kuroe.Flow.Execution.ExecutionSnapshot snapshot = found.Value.Execution;

            terminal.Page("主菜单", "任务", $"{task.Id} {task.Title}", $"{snapshot.Id} {snapshot.Request.Target}");
            executionView.Print(task, snapshot);
            terminal.NewLine();

            List<Item> items = [Refresh];
            if (snapshot.ContinuedFrom is { } upstream)
            {
                items.Add(new Item($"↑ 执行 #{upstream.Value}", "execution", Execution: upstream.Value));
            }

            if (snapshot.State is not (Kuroe.Flow.Execution.ExecutionState.Succeeded
                or Kuroe.Flow.Execution.ExecutionState.Failed
                or Kuroe.Flow.Execution.ExecutionState.Interrupted)
                && snapshot.StartedAt is not null)
            {
                items.Add(new Item("× 停止该执行", "stopExecution", Execution: executionId));
            }

            items.Add(Back);

            Item picked = Choose("执行详情", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "execution" when picked.Execution is { } previous:
                    executionId = previous;
                    break;

                case "stopExecution":
                    StopExecution(executionId);
                    break;
            }
        }
    }

    /// <summary>按执行号取执行与其所属任务的快照。</summary>
    private ErrorOr<(TaskSnapshot Task, Kuroe.Flow.Execution.ExecutionSnapshot Execution)> FindExecution(int id)
    {
        ErrorOr<Kuroe.Flow.Execution.Execution> execution = registry.FindExecution(
            new Kuroe.Flow.Execution.ExecutionId(id));
        if (execution.IsError)
        {
            return execution.ErrorsOrEmptyList;
        }

        ErrorOr<WorkTask> owner = registry.Find(new TaskId(execution.Value.Request.Owner.Value));
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        Kuroe.Flow.Execution.ExecutionSnapshot snapshot = owner.Value.Executions
            .Single(candidate => candidate.Id.Value == id)
            .Snapshot();

        return (owner.Value.Snapshot(), snapshot);
    }

    /// <summary>终止整个任务不可恢复，选择后要求再次确认，默认停在保留任务的选项。</summary>
    private void ConfirmStopTask(TaskId id)
    {
        if (Confirmation.Ask(terminal, $"终止{id}？", "× 确认终止整个任务", "← 返回，保留任务"))
        {
            StopTask(id);
        }
    }

    private void AddTaskActions(List<Item> items, TaskSnapshot task)
    {
        if (task.State is TaskState.Done or TaskState.Terminated)
        {
            return;
        }

        if (task.State == TaskState.Paused)
        {
            items.Add(new Item("▶ 恢复任务", "resumeTask", Task: task.Id));

            if (task.ExecutableStates.Any(state => state.State == NodeState.Blocked))
            {
                items.Add(new Item("* 重新执行被阻塞的节点，放弃恢复，保留恢复数据", "rerun", Task: task.Id));
            }

            items.Add(new Item("× 终止整个任务", "stopTask", Task: task.Id));

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
            if (task.ExecutableStates.Any(state => state.CanRestore))
            {
                items.Add(new Item("▶ 恢复任务", "resumeTask", Task: task.Id));
            }

            items.Add(new Item("* 重新执行被阻塞的节点", "rerun", Task: task.Id));
        }

        items.Add(new Item("Ⅱ 暂停任务", "pauseTask", Task: task.Id));
        items.Add(new Item("× 终止整个任务", "stopTask", Task: task.Id));
    }

    private Item Choose(string title, IReadOnlyList<Item> items) =>
        terminal.Prompt(new MenuPrompt<Item>(title, items, item => item.Label, Back));

    private static string ExecutionItemLabel(GraphExecutionSnapshot execution) =>
        $"› {ViewLabels.ExecutionLabel(execution)}";

    private static string TaskLabel(TaskSnapshot task) =>
        $"#{task.Id.Short} {task.Title} · {ViewLabels.Of(task.State)}";
}

