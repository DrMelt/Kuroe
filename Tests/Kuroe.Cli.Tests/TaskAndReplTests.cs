using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>任务命令族与斜杠命令分发。</summary>
public sealed class TaskAndReplTests
{
    [Fact]
    public void Task_new_submits_and_reports()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "补齐 README"]);

        Assert.Contains("已提交 任务 #1", cli.Output.Output);
        Assert.Single(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_new_with_blank_flow_falls_back_to_default()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "", "补齐 README"]);

        Assert.Contains("已提交 任务 #1", cli.Output.Output);
        Assert.Single(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_show_prints_the_detail()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        cli.Tasks.Run(["/task", "show", "1"]);

        Assert.Contains("任务 #1", cli.Output.Output);
        Assert.Contains("目标：补齐 README", cli.Output.Output);
    }

    [Fact]
    public void Task_show_lists_every_split_source_in_node_table_order()
    {
        using CliSession cli = new(flowsJson: TestFlows.TwoSplitFlow);
        cli.Tasks.Run(["/task", "new", "目标"]);
        Assert.Equal(TaskState.Done, cli.Environment.Settle(new TaskId(1)).State);

        cli.Tasks.Run(["/task", "show", "1"]);

        int first = cli.Output.Output.IndexOf("在节点「整体/计划甲」交回", StringComparison.Ordinal);
        int second = cli.Output.Output.IndexOf("在节点「整体/计划乙」交回", StringComparison.Ordinal);
        Assert.True(first >= 0 && second > first, cli.Output.Output);
    }

    [Fact]
    public void Task_approve_without_awaiting_approval_rejects()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "补齐 README"]);

        cli.Tasks.Run(["/task", "approve", "1"]);

        Assert.Contains("没有等待批准的审批节点", cli.ErrorsOut.Output);
        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Task_commands_shape_completes_the_task()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "clear"]);

        Assert.Contains("已丢掉 1 个任务。", cli.Output.Output);
        Assert.Empty(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_approve_targets_a_named_approval_node()
    {
        using CliSession cli = new(flowsJson: TestFlows.ReviewPerItemFlow);
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        TaskSnapshot parked = cli.Environment.Settle(new TaskId(1));
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve));

        cli.Tasks.Run(["/task", "approve", "1", "@整体/审阅"]);
        TaskSnapshot done = cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, done.State);
    }

    [Fact]
    public void Task_answer_requires_node_path_when_multiple_inputs_wait()
    {
        using CliSession cli = new(flowsJson: TestFlows.TwoInputFlow);
        cli.Tasks.Run(["/task", "new", "目标"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        // 文本首词命中节点标签不会隐式点名，未点名回答仍被拒
        cli.Tasks.Run(["/task", "answer", "1", "输入二", "内容"]);
        Assert.Contains("有 2 个输入节点等待回答", cli.ErrorsOut.Output);

        cli.Tasks.Run(["/task", "answer", "1", "@整体/输入二", "乙"]);
        cli.Tasks.Run(["/task", "answer", "1", "@整体/输入一", "甲"]);
        cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_answer_without_text_shows_usage_and_keeps_waiting()
    {
        using CliSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Run(["/task", "answer", "1"]);

        Assert.Contains("用法：/task", cli.Output.Output);
        Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer));
    }

    [Fact]
    public void Task_answer_with_single_waiting_keeps_leading_node_label_in_text()
    {
        using CliSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Run(["/task", "answer", "1", "用户输入", "需要", "补充多语言"]);
        cli.Environment.Settle(new TaskId(1));

        Assert.Contains(cli.Environment.Snapshot(new TaskId(1)).Executables
            .Single(entry => entry.Executable.Label.Value == "实施")
            .Runs.Single().Context.Seed,
            message => message.Text.Contains("用户输入 需要 补充多语言"));
    }

    [Fact]
    public void Task_approve_and_answer_work_side_by_side_on_parallel_waits()
    {
        using CliSession cli = new(flowsJson: TestFlows.ParallelInputAndReviewFlow);
        cli.Tasks.Run(["/task", "new", "目标"]);
        cli.Environment.Wait(new TaskId(1), _ =>
            cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0
            && cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count > 0);

        cli.Tasks.Run(["/task", "approve", "1"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0
            && cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count == 0);

        cli.Tasks.Run(["/task", "answer", "1", "用户输入", "补充背景"]);
        cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_answer_reports_the_receipt()
    {
        using CliSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Run(["/task", "answer", "1", "需要", "补充多语言"]);

        Assert.Contains("任务 #1 已收到回答，继续下一步。", cli.Output.Output);
    }

    [Fact]
    public void Repl_unknown_command_warns()
    {
        using CliSession cli = new();
        cli.Repl.Execute("/zzz");

        Assert.Contains("未知命令 /zzz", cli.Output.Output);
    }

    [Fact]
    public void Repl_reset_restarts_dialogue()
    {
        using CliSession cli = new();
        cli.Repl.Execute("/reset");

        Assert.Contains("对话已重置", cli.Output.Output);
    }

    [Fact]
    public void Repl_exit_requests_termination()
    {
        using CliSession cli = new();
        Assert.True(cli.Repl.Execute("/exit"));
    }

    [Fact]
    public void Repl_other_command_does_not_terminate()
    {
        using CliSession cli = new();
        Assert.False(cli.Repl.Execute("/help"));
    }

    [Fact]
    public void Repl_help_lists_the_command_families()
    {
        using CliSession cli = new();
        cli.Repl.Execute("/help");

        Assert.Contains("/task", cli.Output.Output);
        Assert.Contains("/flow", cli.Output.Output);
        Assert.Contains("/provider", cli.Output.Output);
    }

    [Fact]
    public void Task_list_prints_the_hint_when_empty_then_rows()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "list"]);
        Assert.Contains("还没有任务", cli.Output.Output);

        cli.Tasks.Run(["/task", "new", "补齐 README"]);

        cli.Tasks.Run(["/task", "list"]);
        Assert.Contains("#1", cli.Output.Output);
        Assert.Contains("补齐 README", cli.Output.Output);
    }

    [Fact]
    public void Task_run_prints_the_context_and_process()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "补齐 README"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "run", "1"]);

        Assert.Contains("run #1", cli.Output.Output);
        Assert.Contains("指令：", cli.Output.Output);
        Assert.Contains("上下文来源", cli.Output.Output);
        Assert.Contains("过程：", cli.Output.Output);
    }

    [Fact]
    public void Task_title_renames_the_task()
    {
        using CliSession cli = new();
        cli.Tasks.Run(["/task", "new", "补齐 README"]);

        cli.Tasks.Run(["/task", "title", "1", "文档", "补齐"]);

        Assert.Contains("已改标题。", cli.Output.Output);
        Assert.Equal("文档 补齐", cli.Environment.Snapshot(new TaskId(1)).Title);
    }

    [Fact]
    public void Task_rework_resumes_a_blocked_task()
    {
        using CliSession cli = new(flowsJson: TestFlows.BlockingFlow);
        cli.Tasks.Run(["/task", "new", "目标"]);
        cli.Environment.Settle(new TaskId(1));
        Assert.Equal(TaskState.Blocked, cli.Environment.Snapshot(new TaskId(1)).State);

        cli.Tasks.Run(["/task", "rework", "1"]);

        Assert.Contains("任务 #1 已返工。", cli.Output.Output);
    }

    [Fact]
    public void Task_stop_cancels_the_task()
    {
        using CliSession cli = new();
        cli.Environment.Executor.DelayMs = 20000;
        cli.Tasks.Run(["/task", "new", "长任务"]);
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveRuns > 0);

        cli.Tasks.Run(["/task", "stop", "1"]);

        Assert.Contains("任务 #1 已取消。", cli.Output.Output);
        Assert.Equal(TaskState.Canceled, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_stop_run_cancels_a_single_run()
    {
        using CliSession cli = new();
        cli.Environment.Executor.DelayMs = 20000;
        cli.Tasks.Run(["/task", "new", "长任务"]);
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveRuns > 0);

        cli.Tasks.Run(["/task", "stop", "run", "1"]);

        Assert.Contains("run #1 已取消。", cli.Output.Output);
    }

    [Fact]
    public void Task_show_with_a_non_number_warns_and_shows_usage()
    {
        using CliSession cli = new();

        cli.Tasks.Run(["/task", "show", "abc"]);

        Assert.Contains("任务号要写成数字，收到 abc。", cli.Output.Output);
        Assert.Contains("用法：/task", cli.Output.Output);
    }

    [Fact]
    public void Task_new_without_a_target_after_the_flow_option_shows_usage()
    {
        using CliSession cli = new();

        cli.Tasks.Run(["/task", "new", "--flow"]);

        Assert.Contains("用法：/task new --flow <流程> <目标>", cli.Output.Output);
    }

    [Fact]
    public void Task_new_with_a_named_flow_submits_it()
    {
        using CliSession cli = new();

        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        Assert.Contains("已提交 任务 #1（流程 默认）", cli.Output.Output);
        Assert.Single(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Repl_dispatches_to_every_command_family()
    {
        using CliSession cli = new();

        cli.Repl.Execute("/config");
        cli.Repl.Execute("/provider list");
        cli.Repl.Execute("/model");
        cli.Repl.Execute("/catalog list");
        cli.Repl.Execute("/task list");
        cli.Repl.Execute("/flow list");
        cli.Repl.Execute("/tool list");
        cli.Repl.Execute("/set Runtime:Temperature 0.5");
        cli.Repl.Execute("/unset Runtime:Temperature");

        Assert.Contains("用户层文件：", cli.Output.Output);
        Assert.Contains("提供商：", cli.Output.Output);
        Assert.Contains("当前模型 fake。", cli.Output.Output);
        Assert.Contains("还没有任务", cli.Output.Output);
        Assert.Contains("制定计划", cli.Output.Output);
        Assert.Contains("没有配置命令工具", cli.Output.Output);
        Assert.Contains("已保存并生效。", cli.Output.Output);
    }

    [Fact]
    public void Repl_command_token_is_case_insensitive()
    {
        using CliSession cli = new();

        cli.Repl.Execute("/TASK LIST");

        Assert.Contains("还没有任务", cli.Output.Output);
    }
}
