using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Pending;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>任务命令族与斜杠命令分发。</summary>
public sealed class TaskAndReplTests
{
    [Fact]
    public void Task_new_submits_and_reports()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        Assert.Contains("已提交 任务 #1", cli.Output.Output);
        Assert.Single(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_new_with_blank_flow_rejects()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "", "补齐 README"]);

        Assert.Contains("流程名不能为空。", cli.ErrorsOut.Output);
        Assert.Empty(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_tree_view_prints_only_the_node_tree()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
        cli.Environment.Settle(new TaskId(1));

        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        new TaskTreeView(terminal, cli.Environment.Pendings).Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("节点", output.Output);
        Assert.DoesNotContain('┌', output.Output);
        Assert.DoesNotContain("目标：", output.Output);
        Assert.DoesNotContain("流程：", output.Output);
        Assert.DoesNotContain("最近", output.Output);
    }

    [Fact]
    public void Task_show_prints_the_overview_and_execution_list()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        Assert.Equal(TaskState.Done, cli.Environment.Settle(new TaskId(1)).State);

        cli.Tasks.Run(["/task", "show", "1"]);

        Assert.Contains("整体/制定计划", cli.Output.Output);
        Assert.Contains("执行 #1", cli.Output.Output);
        Assert.Contains("过程记录", cli.Output.Output);
        Assert.Contains("── 任务 #1 目标 ", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact]
    public void Task_show_prints_pending_nodes_without_the_tree()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "show", "1"]);

        Assert.Contains("待批准", cli.Output.Output);
        Assert.Contains("确认计划", cli.Output.Output);
        Assert.Contains("过程记录", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact]
    public void Task_tree_prints_a_tree_and_marks_waiting_nodes()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "tree", "1"]);

        Assert.Contains("├", cli.Output.Output);
        Assert.Contains("└", cli.Output.Output);
        Assert.Contains("待批准：确认计划", cli.Output.Output);
        Assert.Contains("── 任务 #1 目标 ", cli.Output.Output);
        Assert.DoesNotContain("过程记录", cli.Output.Output);
    }

    [Fact]
    public void Task_tree_view_leaves_the_journal_to_the_command()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        TaskTreeView tree = new(cli.Terminal, cli.Environment.Pendings);
        tree.Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("节点", cli.Output.Output);
        Assert.Contains("已完成", cli.Output.Output);
        Assert.DoesNotContain("过程记录", cli.Output.Output);
    }

    [Fact]
    public void Task_browser_opens_the_overview_without_the_tree()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Run(["/task"]);

        Assert.Contains("任务 1 个", cli.Output.Output);
        Assert.Contains("── 任务列表 › 任务 #1 目标 ", cli.Output.Output);
        Assert.Contains("查看节点树", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact]
    public void Task_browser_marks_the_task_and_execution_levels()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Run(["/task"]);

        Assert.Contains("› 执行 #1 ", cli.Output.Output);
        Assert.Contains("概要", cli.Output.Output);

        string[] taskPages = [.. cli.Output.Output.Replace("\r", string.Empty).Split('\n')
            .Where(line => line.StartsWith("── 任务列表 › 任务 #1 目标 ", StringComparison.Ordinal))
            .Where(line => !line.Contains("› 执行 #1", StringComparison.Ordinal))];
        Assert.Equal(2, taskPages.Length);
    }

    [Fact]
    public void Task_browser_keeps_the_task_when_the_stop_confirmation_is_not_accepted()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "长任务"]);
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Run(["/task"]);

        Assert.Contains("取消任务 #1？", cli.Output.Output);
        Assert.Contains("确认取消整个任务", cli.Output.Output);
        Assert.Equal(TaskState.Running, cli.Environment.Snapshot(new TaskId(1)).State);

        cli.Tasks.Run(["/task", "stop", "1"]);
    }

    [Fact]
    public void Task_browser_stops_the_task_after_confirmation()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "长任务"]);
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Run(["/task"]);

        Assert.Contains("确认取消整个任务", cli.Output.Output);
        Assert.Equal(TaskState.Canceled, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_browser_lists_waiting_inputs_and_answers_the_selected_node()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.TwoInputFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count == 2);

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("reply");
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Run(["/task"]);

        Assert.Contains("选择要回答的节点", cli.Output.Output);
        Assert.Contains("整体/输入二 · 待输入：问题二", cli.Output.Output);
        NodePath left = Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer));
        Assert.Equal("整体/输入一", left.Value);
    }

    [Fact]
    public void Task_list_shows_the_creation_time_without_progress()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "list"]);

        Assert.Contains("创建时间", cli.Output.Output);
        Assert.DoesNotContain("进度", cli.Output.Output);
    }

    [Fact]
    public void Task_list_shows_waiting_node_count()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count > 0);

        cli.Tasks.Run(["/task", "list"]);

        string row = cli.Output.Output
            .Split('\n')
            .Last(line => line.Contains("#1 "));
        Assert.Contains("待处理", cli.Output.Output);
        Assert.Matches(@"执行中\s+1\s+\d{2}:\d{2}:\d{2}", row);
        Assert.DoesNotContain("整体/审阅", cli.Output.Output);
    }

    [Fact]
    public void Task_execution_renders_input_sources()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "execution", "1"]);

        Assert.Contains("输入取值", cli.Output.Output);
        Assert.Contains("流程起点端口「目标」", cli.Output.Output);
        Assert.Contains("来源", cli.Output.Output);
        Assert.Contains("── 执行 #1 ", cli.Output.Output);
    }

    [Fact]
    public void Task_approve_without_awaiting_approval_rejects()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        cli.Tasks.Run(["/task", "approve", "1"]);

        Assert.Contains("没有等待批准的审批节点", cli.ErrorsOut.Output);
        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Task_commands_shape_completes_the_task()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "clear"]);

        Assert.Contains("已丢掉 1 个任务。", cli.Output.Output);
        Assert.Empty(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_approve_targets_a_named_approval_node()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
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
        using ConsoleSession cli = new(flowsJson: TestFlows.TwoInputFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
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
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Run(["/task", "answer", "1"]);

        Assert.Contains("用法：/task", cli.Output.Output);
        Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer));
    }

    [Fact]
    public void Task_answer_with_single_waiting_keeps_leading_node_label_in_text()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Run(["/task", "answer", "1", "用户输入", "需要", "补充多语言"]);
        cli.Environment.Settle(new TaskId(1));

        Assert.Contains(cli.Environment.Snapshot(new TaskId(1)).Executables
            .Single(entry => entry.Executable.Label.Value == "实施")
            .Executions.Single().Input.Inputs,
            value => value.Text.Contains("用户输入 需要 补充多语言"));
    }

    [Fact]
    public void Task_approve_and_answer_work_side_by_side_on_parallel_waits()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ParallelInputAndReviewFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
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
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Run(["/task", "answer", "1", "需要", "补充多语言"]);

        Assert.Contains("任务 #1 已收到回答，继续下一步。", cli.Output.Output);
    }

    [Fact]
    public void Repl_unknown_command_warns()
    {
        using ConsoleSession cli = new();
        cli.Repl.Execute("/zzz");

        Assert.Contains("未知命令 /zzz", cli.Output.Output);
    }

    [Fact]
    public void Repl_reset_restarts_dialogue()
    {
        using ConsoleSession cli = new();
        cli.Repl.Execute("/reset");

        Assert.Contains("对话已重置", cli.Output.Output);
    }

    [Fact]
    public void Repl_exit_requests_termination()
    {
        using ConsoleSession cli = new();
        Assert.True(cli.Repl.Execute("/exit"));
    }

    [Fact]
    public void Repl_other_command_does_not_terminate()
    {
        using ConsoleSession cli = new();
        Assert.False(cli.Repl.Execute("/help"));
    }

    [Fact]
    public void Repl_help_lists_the_command_families()
    {
        using ConsoleSession cli = new();
        cli.Repl.Execute("/help");

        Assert.Contains("/task", cli.Output.Output);
        Assert.Contains("/flow", cli.Output.Output);
        Assert.Contains("/provider", cli.Output.Output);
    }

    [Fact]
    public void Task_list_prints_the_hint_when_empty_then_rows()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "list"]);
        Assert.Contains("还没有任务", cli.Output.Output);

        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        cli.Tasks.Run(["/task", "list"]);
        Assert.Contains("#1", cli.Output.Output);
        Assert.Contains("补齐 README", cli.Output.Output);
    }

    [Fact]
    public void Task_execution_prints_the_context_and_process()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Run(["/task", "execution", "1"]);

        Assert.Contains("执行 #1", cli.Output.Output);
        Assert.Contains("输入取值", cli.Output.Output);
        Assert.Contains("过程记录", cli.Output.Output);
    }

    [Fact]
    public void Task_title_renames_the_task()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        cli.Tasks.Run(["/task", "title", "1", "文档", "补齐"]);

        Assert.Contains("已改标题。", cli.Output.Output);
        Assert.Equal("文档 补齐", cli.Environment.Snapshot(new TaskId(1)).Title);
    }

    [Fact]
    public void Task_rework_resumes_a_blocked_task()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.BlockingFlow);
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "目标"]);
        cli.Environment.Settle(new TaskId(1));
        Assert.Equal(TaskState.Blocked, cli.Environment.Snapshot(new TaskId(1)).State);

        cli.Tasks.Run(["/task", "rework", "1"]);

        Assert.Contains("任务 #1 已返工。", cli.Output.Output);
    }

    [Fact]
    public void Task_stop_cancels_the_task()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "长任务"]);
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Tasks.Run(["/task", "stop", "1"]);

        Assert.Contains("任务 #1 已取消。", cli.Output.Output);
        Assert.Equal(TaskState.Canceled, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_stop_execution_cancels_a_single_execution()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Run(["/task", "new", "--flow", "默认", "长任务"]);
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Tasks.Run(["/task", "stop", "execution", "1"]);

        Assert.Contains("执行 #1 已取消。", cli.Output.Output);
    }

    [Fact]
    public void Task_show_with_a_non_number_warns_and_shows_usage()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Run(["/task", "show", "abc"]);

        Assert.Contains("任务号要写成数字，收到 abc。", cli.Output.Output);
        Assert.Contains("用法：/task", cli.Output.Output);
    }

    [Fact]
    public void Task_new_without_the_flow_option_shows_usage()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Run(["/task", "new", "补齐 README"]);

        Assert.Contains("用法：/task new --flow <流程> <目标>", cli.Output.Output);
    }

    [Fact]
    public void Task_new_without_a_target_after_the_flow_option_shows_usage()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Run(["/task", "new", "--flow"]);

        Assert.Contains("用法：/task new --flow <流程> <目标>", cli.Output.Output);
    }

    [Fact]
    public void Task_new_with_a_named_flow_submits_it()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Run(["/task", "new", "--flow", "默认", "补齐 README"]);

        Assert.Contains("已提交 任务 #1（流程 默认）", cli.Output.Output);
        Assert.Single(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public async Task Task_list_does_not_show_the_dialogue_task()
    {
        using ConsoleSession cli = new();

        var reply = await cli.Environment.Dialogue.ReplyAsync("你好", null);
        cli.Tasks.Run(["/task", "list"]);

        Assert.False(reply.IsError);
        Assert.Contains("还没有任务", cli.Output.Output);
    }

    [Fact]
    public async Task Task_stop_cannot_reach_the_dialogue_task()
    {
        using ConsoleSession cli = new();

        var reply = await cli.Environment.Dialogue.ReplyAsync("你好", null);
        cli.Tasks.Run(["/task", "stop", "1"]);

        Assert.False(reply.IsError);
        Assert.Contains("没有任务 #1", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Repl_dispatches_to_every_command_family()
    {
        using ConsoleSession cli = new();

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
        Assert.Contains("提供商", cli.Output.Output);
        Assert.Contains("当前模型 fake。", cli.Output.Output);
        Assert.Contains("还没有任务", cli.Output.Output);
        Assert.Contains("执行节点", cli.Output.Output);
        Assert.Contains("没有配置命令工具", cli.Output.Output);
        Assert.Contains("已保存并生效。", cli.Output.Output);
    }

    [Fact]
    public void Repl_command_token_is_case_insensitive()
    {
        using ConsoleSession cli = new();

        cli.Repl.Execute("/TASK LIST");

        Assert.Contains("还没有任务", cli.Output.Output);
    }

    [Fact]
    public void List_commands_render_inside_a_view_box()
    {
        using ConsoleSession cli = new();

        cli.Repl.Execute("/help");
        cli.Repl.Execute("/flow list");
        cli.Repl.Execute("/provider list");

        Assert.Contains('┌', cli.Output.Output);
        Assert.Contains("命令", cli.Output.Output);
        Assert.Contains("流程 1 条", cli.Output.Output);
        Assert.Contains("提供商", cli.Output.Output);
    }
}
