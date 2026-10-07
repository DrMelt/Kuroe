using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>任务命令族与斜杠命令分发。</summary>
public sealed class TaskAndReplTests
{
    [Fact]
    public void Task_new_submits_and_reports()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);

        Assert.Contains("已提交 任务 #1", ui.Output.Output);
        Assert.Single(ui.Harness.Registry.Snapshots());
    }

    [Fact]
    public void Task_new_with_blank_flow_falls_back_to_default()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "--flow", "", "补齐 README"]);

        Assert.Contains("已提交 任务 #1", ui.Output.Output);
        Assert.Single(ui.Harness.Registry.Snapshots());
    }

    [Fact]
    public void Task_show_prints_the_detail()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        ui.Tasks.Run(["/task", "show", "1"]);

        Assert.Contains("任务 #1", ui.Output.Output);
        Assert.Contains("目标：补齐 README", ui.Output.Output);
    }

    [Fact]
    public void Task_approve_without_review_gate_rejects()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);

        ui.Tasks.Run(["/task", "approve", "1"]);

        Assert.Contains("没有等待批准的产出", ui.ErrorsOut.Output);
        Assert.Contains("未生效。", ui.ErrorsOut.Output);
    }

    [Fact]
    public void Task_commands_shape_completes_the_task()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        ui.Harness.Settle(new TaskId(1));

        ui.Tasks.Run(["/task", "clear"]);

        Assert.Contains("已丢掉 1 个任务。", ui.Output.Output);
        Assert.Empty(ui.Harness.Registry.Snapshots());
    }

    [Fact]
    public void Task_approve_run_approves_only_that_run()
    {
        using Ui ui = new(flowsJson: TestFlows.ReviewPerItemFlow);
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        ui.Harness.Settle(new TaskId(1));

        TaskSnapshot parked = ui.Harness.Snapshot(new TaskId(1));
        RunId first = parked.Executables[1].Runs[0].Id;
        ui.Tasks.Run(["/task", "approve", "1", "run", first.Value.ToString()]);

        ExecutableStateSnapshot still = Assert.Single(ui.Harness.Snapshot(new TaskId(1)).ExecutableStates,
            state => state.Index == parked.Executables[1].Index);
        Assert.Single(still.AwaitingRuns);
    }

    [Fact]
    public void Task_answer_requires_node_name_when_multiple_inputs_wait()
    {
        using Ui ui = new(flowsJson: TestFlows.TwoInputFlow);
        ui.Tasks.Run(["/task", "new", "目标"]);
        ui.Harness.Wait(new TaskId(1), snapshot => snapshot.State == TaskState.AwaitingInput);

        // 文本首词命中节点名不会隐式点名，未点名回答仍被拒
        ui.Tasks.Run(["/task", "answer", "1", "输入二", "内容"]);
        Assert.Contains("有 2 个输入节点等待回答", ui.ErrorsOut.Output);

        ui.Tasks.Run(["/task", "answer", "1", "@输入二", "乙"]);
        ui.Tasks.Run(["/task", "answer", "1", "@输入一", "甲"]);
        ui.Harness.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, ui.Harness.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_answer_with_single_waiting_keeps_leading_node_name_in_text()
    {
        using Ui ui = new(flowsJson: TestFlows.InputFlow);
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        ui.Harness.Wait(new TaskId(1), snapshot => snapshot.State == TaskState.AwaitingInput);

        ui.Tasks.Run(["/task", "answer", "1", "用户输入", "需要", "补充多语言"]);
        ui.Harness.Settle(new TaskId(1));

        Assert.Contains(ui.Harness.Snapshot(new TaskId(1)).Executables
            .Single(entry => entry.Executable.Name.Value == "实施")
            .Runs.Single().Context.Seed,
            message => message.Text.Contains("用户输入 需要 补充多语言"));
    }

    [Fact]
    public void Task_approve_and_answer_work_side_by_side_on_parallel_waits()
    {
        using Ui ui = new(flowsJson: TestFlows.ParallelInputAndReviewFlow);
        ui.Tasks.Run(["/task", "new", "目标"]);
        ui.Harness.Wait(new TaskId(1), snapshot =>
            snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput)
            && snapshot.Containers.Any(container => container.Name == "审查分支" && container.State == NodeState.AwaitingApproval));

        ui.Tasks.Run(["/task", "approve", "1"]);
        ui.Harness.Wait(new TaskId(1), snapshot => snapshot.State == TaskState.AwaitingInput
            && snapshot.Containers.Single(container => container.Name == "审查分支").State == NodeState.Done);

        ui.Tasks.Run(["/task", "answer", "1", "用户输入", "补充背景"]);
        ui.Harness.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, ui.Harness.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Repl_unknown_command_warns()
    {
        using Ui ui = new();
        ui.Repl.Execute("/zzz");

        Assert.Contains("未知命令 /zzz", ui.Output.Output);
    }

    [Fact]
    public void Repl_reset_without_task_hints()
    {
        using Ui ui = new();
        ui.Repl.Execute("/reset");

        Assert.Contains("还没有任务", ui.Output.Output);
    }

    [Fact]
    public void Repl_help_lists_the_command_families()
    {
        using Ui ui = new();
        ui.Repl.Execute("/help");

        Assert.Contains("/task", ui.Output.Output);
        Assert.Contains("/flow", ui.Output.Output);
        Assert.Contains("/provider", ui.Output.Output);
    }

    [Fact]
    public void Repl_prompt_renders_root_without_active_task()
    {
        using Ui ui = new();

        Assert.Equal("root > ", Repl.RenderPrompt(ui.Harness.Registry));
    }

    [Fact]
    public void Repl_prompt_renders_task_and_node_progress()
    {
        using Ui ui = new();
        TaskId id = ui.Harness.Submit("补齐 README");
        ui.Harness.Settle(id);

        string prompt = Repl.RenderPrompt(ui.Harness.Registry);

        Assert.Equal($"任务 #{id.Value} · 节点 {ui.Harness.Snapshot(id).FrontierNodes}/{ui.Harness.Snapshot(id).TotalExecutableNodes} > ", prompt);
        Assert.Contains($"任务 #{id.Value}", prompt);
    }
}
