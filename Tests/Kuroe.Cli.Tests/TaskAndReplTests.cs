using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.Waiting;
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
    public void Task_approve_without_awaiting_approval_rejects()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);

        ui.Tasks.Run(["/task", "approve", "1"]);

        Assert.Contains("没有等待批准的审批节点", ui.ErrorsOut.Output);
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
    public void Task_approve_targets_a_named_approval_node()
    {
        using Ui ui = new(flowsJson: TestFlows.ReviewPerItemFlow);
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        TaskSnapshot parked = ui.Harness.Settle(new TaskId(1));
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(ui.Harness.Awaiting(new TaskId(1), AwaitKind.Approve));

        ui.Tasks.Run(["/task", "approve", "1", "@审阅"]);
        TaskSnapshot done = ui.Harness.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Task_answer_requires_node_name_when_multiple_inputs_wait()
    {
        using Ui ui = new(flowsJson: TestFlows.TwoInputFlow);
        ui.Tasks.Run(["/task", "new", "目标"]);
        ui.Harness.Wait(new TaskId(1), _ => ui.Harness.Awaiting(new TaskId(1), AwaitKind.Answer).Count > 0);

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
        ui.Harness.Wait(new TaskId(1), _ => ui.Harness.Awaiting(new TaskId(1), AwaitKind.Answer).Count > 0);

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
        ui.Harness.Wait(new TaskId(1), _ =>
            ui.Harness.Awaiting(new TaskId(1), AwaitKind.Answer).Count > 0
            && ui.Harness.Awaiting(new TaskId(1), AwaitKind.Approve).Count > 0);

        ui.Tasks.Run(["/task", "approve", "1"]);
        ui.Harness.Wait(new TaskId(1), _ => ui.Harness.Awaiting(new TaskId(1), AwaitKind.Answer).Count > 0
            && ui.Harness.Awaiting(new TaskId(1), AwaitKind.Approve).Count == 0);

        ui.Tasks.Run(["/task", "answer", "1", "用户输入", "补充背景"]);
        ui.Harness.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, ui.Harness.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_answer_reports_the_receipt()
    {
        using Ui ui = new(flowsJson: TestFlows.InputFlow);
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        ui.Harness.Wait(new TaskId(1), _ => ui.Harness.Awaiting(new TaskId(1), AwaitKind.Answer).Count > 0);

        ui.Tasks.Run(["/task", "answer", "1", "需要", "补充多语言"]);

        Assert.Contains("任务 #1 已收到回答，继续下一步。", ui.Output.Output);
    }

    [Fact]
    public void Repl_unknown_command_warns()
    {
        using Ui ui = new();
        ui.Repl.Execute("/zzz");

        Assert.Contains("未知命令 /zzz", ui.Output.Output);
    }

    [Fact]
    public void Repl_reset_restarts_dialogue()
    {
        using Ui ui = new();
        ui.Repl.Execute("/reset");

        Assert.Contains("对话已重置", ui.Output.Output);
    }

    [Fact]
    public void Repl_exit_requests_termination()
    {
        using Ui ui = new();
        Assert.True(ui.Repl.Execute("/exit"));
    }

    [Fact]
    public void Repl_other_command_does_not_terminate()
    {
        using Ui ui = new();
        Assert.False(ui.Repl.Execute("/help"));
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
    public void Task_list_prints_the_hint_when_empty_then_rows()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "list"]);
        Assert.Contains("还没有任务", ui.Output.Output);

        ui.Tasks.Run(["/task", "new", "补齐 README"]);

        ui.Tasks.Run(["/task", "list"]);
        Assert.Contains("#1", ui.Output.Output);
        Assert.Contains("补齐 README", ui.Output.Output);
    }

    [Fact]
    public void Task_run_prints_the_context_and_process()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);
        ui.Harness.Settle(new TaskId(1));

        ui.Tasks.Run(["/task", "run", "1"]);

        Assert.Contains("run #1", ui.Output.Output);
        Assert.Contains("指令：", ui.Output.Output);
        Assert.Contains("上下文来源", ui.Output.Output);
        Assert.Contains("过程：", ui.Output.Output);
    }

    [Fact]
    public void Task_title_renames_the_task()
    {
        using Ui ui = new();
        ui.Tasks.Run(["/task", "new", "补齐 README"]);

        ui.Tasks.Run(["/task", "title", "1", "文档", "补齐"]);

        Assert.Contains("已改标题。", ui.Output.Output);
        Assert.Equal("文档 补齐", ui.Harness.Snapshot(new TaskId(1)).Title);
    }

    [Fact]
    public void Task_rework_resumes_a_blocked_task()
    {
        using Ui ui = new(flowsJson: TestFlows.BlockingFlow);
        ui.Tasks.Run(["/task", "new", "目标"]);
        ui.Harness.Settle(new TaskId(1));
        Assert.Equal(TaskState.Blocked, ui.Harness.Snapshot(new TaskId(1)).State);

        ui.Tasks.Run(["/task", "rework", "1"]);

        Assert.Contains("任务 #1 已返工。", ui.Output.Output);
    }

    [Fact]
    public void Task_stop_cancels_the_task()
    {
        using Ui ui = new();
        ui.Harness.Executor.DelayMs = 20000;
        ui.Tasks.Run(["/task", "new", "长任务"]);
        ui.Harness.Wait(new TaskId(1), snapshot => snapshot.LiveRuns > 0);

        ui.Tasks.Run(["/task", "stop", "1"]);

        Assert.Contains("任务 #1 已取消。", ui.Output.Output);
        Assert.Equal(TaskState.Canceled, ui.Harness.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_stop_run_cancels_a_single_run()
    {
        using Ui ui = new();
        ui.Harness.Executor.DelayMs = 20000;
        ui.Tasks.Run(["/task", "new", "长任务"]);
        ui.Harness.Wait(new TaskId(1), snapshot => snapshot.LiveRuns > 0);

        ui.Tasks.Run(["/task", "stop", "run", "1"]);

        Assert.Contains("run #1 已取消。", ui.Output.Output);
    }

    [Fact]
    public void Task_show_with_a_non_number_warns_and_shows_usage()
    {
        using Ui ui = new();

        ui.Tasks.Run(["/task", "show", "abc"]);

        Assert.Contains("任务号要写成数字，收到 abc。", ui.Output.Output);
        Assert.Contains("用法：/task", ui.Output.Output);
    }

    [Fact]
    public void Task_new_without_a_target_after_the_flow_option_shows_usage()
    {
        using Ui ui = new();

        ui.Tasks.Run(["/task", "new", "--flow"]);

        Assert.Contains("用法：/task new --flow <流程> <目标>", ui.Output.Output);
    }

    [Fact]
    public void Task_new_with_a_named_flow_submits_it()
    {
        using Ui ui = new();

        ui.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        Assert.Contains("已提交 任务 #1（流程 默认）", ui.Output.Output);
        Assert.Single(ui.Harness.Registry.Snapshots());
    }

    [Fact]
    public void Repl_dispatches_to_every_command_family()
    {
        using Ui ui = new();

        ui.Repl.Execute("/config");
        ui.Repl.Execute("/provider list");
        ui.Repl.Execute("/model");
        ui.Repl.Execute("/catalog list");
        ui.Repl.Execute("/task list");
        ui.Repl.Execute("/flow list");
        ui.Repl.Execute("/tool list");
        ui.Repl.Execute("/set Runtime:Temperature 0.5");
        ui.Repl.Execute("/unset Runtime:Temperature");

        Assert.Contains("用户层文件：", ui.Output.Output);
        Assert.Contains("提供商：", ui.Output.Output);
        Assert.Contains("当前模型 fake。", ui.Output.Output);
        Assert.Contains("还没有任务", ui.Output.Output);
        Assert.Contains("制定计划", ui.Output.Output);
        Assert.Contains("没有配置命令工具", ui.Output.Output);
        Assert.Contains("已保存并生效。", ui.Output.Output);
    }

    [Fact]
    public void Repl_command_token_is_case_insensitive()
    {
        using Ui ui = new();

        ui.Repl.Execute("/TASK LIST");

        Assert.Contains("还没有任务", ui.Output.Output);
    }
}
