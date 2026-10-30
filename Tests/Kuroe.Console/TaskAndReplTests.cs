using ApiHub.Shared.Models;
using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Pending;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>任务浏览与终端主循环。</summary>
public sealed partial class TaskAndReplTests
{
    [Fact]
    public void Task_submit_reports_the_task()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");

        Assert.Contains("已提交 任务 00000001", cli.Output.Output);
        Assert.Single(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_submit_with_a_blank_goal_is_rejected()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Submit(new FlowName("默认"), "   ");

        Assert.Contains("开始端口 目标 的输入不能为空。", cli.ErrorsOut.Output);
        Assert.Empty(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_submit_asks_every_start_port()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.MultiStartFlow);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("readme");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("plain");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        string output = cli.Output.Output;
        string[] blocks = output.Split("开始输入");
        Assert.Equal(5, blocks.Length);
        Assert.Contains("readme", blocks[2]);
        Assert.Contains("plain", blocks[3]);
        Assert.Contains("readme", blocks[4]);
        Assert.Contains("plain", blocks[4]);
        Assert.Contains("已提交 任务 00000001", output);
        TaskSnapshot task = cli.Environment.Snapshot(new TaskId(1));
        Assert.Equal(["readme", "plain"], task.Inputs.Values.Select(input => input.Text));
    }

    [Fact]
    public void Task_submit_shows_fixed_start_ports_as_context()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.FixedStartFlow);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("readme");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Tasks.Browse();

        string output = cli.Output.Output;
        Assert.Equal(3, output.Split("开始输入").Length - 1);
        Assert.Contains("固定", output);
        Assert.Contains("风格", output);
        Assert.Contains("简洁", output);
        Assert.Contains("已提交 任务 00000001", output);
        Assert.Equal("readme", cli.Environment.Snapshot(new TaskId(1)).Inputs.Values.Single().Text);
    }

    [Fact]
    public void Task_submit_is_cancelled_at_the_confirmation()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.MultiStartFlow);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("readme");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("plain");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        Assert.Empty(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_overview_prints_the_executions()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        Assert.Equal(TaskState.Done, cli.Environment.Settle(new TaskId(1)).State);

        cli.Overview.Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("整体/制定计划", cli.Output.Output);
        Assert.Contains("执行 #1", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact]
    public void Task_overview_prints_pending_nodes_without_the_tree()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        cli.Overview.Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("待批准", cli.Output.Output);
        Assert.Contains("确认计划", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact]
    public void Task_journal_prints_the_process_records()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Tasks.ShowJournal(new TaskId(1));

        Assert.Contains("过程记录", cli.Output.Output);
        Assert.Contains("── 主菜单 › 任务 › 任务 00000001 目标 › 过程记录", cli.Output.Output);
        Assert.Contains("← 返回上一级", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact]
    public void Task_tree_opens_as_a_page()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Tasks.ShowTree(new TaskId(1));

        Assert.Contains("── 主菜单 › 任务 › 任务 00000001 目标 › 节点树", cli.Output.Output);
        Assert.Contains("← 返回上一级", cli.Output.Output);
        Assert.Contains("├", cli.Output.Output);
    }

    [Fact]
    public void Task_tree_prints_a_tree_and_marks_waiting_nodes()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        new TaskTreeView(terminal, cli.Environment.Pendings).Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("├", output.Output);
        Assert.Contains("└", output.Output);
        Assert.Contains("待批准：确认计划", output.Output);
    }

    [Fact]
    public void Task_browser_opens_the_overview_without_the_tree()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        string output = cli.Output.Output;
        Assert.Contains("任务 1 个", output);
        Assert.Contains("── 主菜单 › 任务 › 任务 00000001 目标 ", output);
        Assert.Contains("查看节点树", output);
        Assert.DoesNotContain("├", output);

        int menu = output.IndexOf("选择任务", StringComparison.Ordinal);
        Assert.True(
            menu >= 0
            && output.IndexOf("~ 刷新", menu, StringComparison.Ordinal) < output.IndexOf("#00000001 目标 · ", menu, StringComparison.Ordinal),
            output);
    }

    [Fact]
    public void Task_browser_marks_the_task_and_execution_levels()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        Assert.Contains("› 执行 #1 ", cli.Output.Output);
        Assert.Contains("概要", cli.Output.Output);

        string[] taskPages = [.. cli.Output.Output.Replace("\r", string.Empty).Split('\n')
            .Where(line => line.StartsWith("── 主菜单 › 任务 › 任务 00000001 目标 ", StringComparison.Ordinal))
            .Where(line => !line.Contains("› 执行 #1", StringComparison.Ordinal))];
        Assert.Equal(2, taskPages.Length);
    }

    [Fact]
    public void Task_browser_keeps_the_task_when_the_stop_confirmation_is_not_accepted()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        for (int i = 0; i < 4; i++)
        {
            cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        }

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        Assert.Contains("终止任务 00000001？", cli.Output.Output);
        Assert.Contains("确认终止整个任务", cli.Output.Output);
        Assert.Equal(TaskState.Running, cli.Environment.Snapshot(new TaskId(1)).State);

        cli.Tasks.StopTask(new TaskId(1));
    }

    [Fact]
    public void Task_browser_stops_the_task_after_confirmation()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        for (int i = 0; i < 4; i++)
        {
            cli.Output.Input.PushKey(ConsoleKey.UpArrow);
        }

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        Assert.Contains("确认终止整个任务", cli.Output.Output);
        Assert.Equal(TaskState.Terminated, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_browser_lists_waiting_inputs_and_answers_the_selected_node()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.TwoInputFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count == 2);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("reply");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        Assert.Contains("选择要回答的节点", cli.Output.Output);
        Assert.Contains("整体/输入二 · 待输入：问题二", cli.Output.Output);
        Assert.Contains("回答等待输入", cli.Output.Output);
        Assert.Contains("提问", cli.Output.Output);
        NodePath left = Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer));
        Assert.Equal("整体/输入一", left.Value);
    }

    [Fact]
    public void Task_browser_submits_a_task_from_the_menu()
    {
        using ConsoleSession cli = new();

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter("menu-submit");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Tasks.Browse();

        Assert.Contains("已提交 任务 00000001", cli.Output.Output);
        Assert.Equal("menu-submit", Assert.Single(cli.Environment.Registry.Snapshots()).Title);
    }

    [Fact]
    public void Task_list_shows_the_creation_time_without_progress()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Tasks.Browse();

        Assert.Contains("创建时间", cli.Output.Output);
        Assert.DoesNotContain("进度", cli.Output.Output);
    }

    [Fact]
    public void Task_list_shows_waiting_node_count()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count > 0);

        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Tasks.Browse();

        string row = cli.Output.Output
            .Split('\n')
            .Last(line => line.Contains("#00000001 ") && MyRegex().IsMatch(line));
        Assert.Contains("待处理", cli.Output.Output);
        Assert.Matches(@"执行中\s+1\s+\d{2}:\d{2}:\d{2}", row);
        Assert.DoesNotContain("整体/审阅", cli.Output.Output);
    }

    [Fact]
    public void Task_execution_renders_input_sources_and_process()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        ExecutionSnapshot execution = cli.Environment.Registry.FindExecution(new ExecutionId(1)).ThrowIfError().Snapshot();
        cli.Tasks.ShowExecution(execution.Id);

        Assert.Contains("执行 #1", cli.Output.Output);
        Assert.Contains("输入取值", cli.Output.Output);
        Assert.Contains("流程起点端口「目标」", cli.Output.Output);
        Assert.Contains("来源", cli.Output.Output);
        Assert.Contains("过程记录", cli.Output.Output);
        Assert.Contains("› 执行 #1 ", cli.Output.Output);
    }

    [Fact]
    public void Task_approve_without_awaiting_approval_rejects()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");

        cli.Tasks.Approve(new TaskId(1));

        Assert.Contains("没有等待批准的审批节点", cli.ErrorsOut.Output);
        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact]
    public void Task_clear_drops_settled_tasks()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Clear();

        Assert.Contains("已丢掉 1 个任务。", cli.Output.Output);
        Assert.Empty(cli.Environment.Registry.Snapshots());
    }

    [Fact]
    public void Task_approve_completes_a_review_task()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        TaskSnapshot parked = cli.Environment.Settle(new TaskId(1));
        Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve));

        cli.Tasks.Approve(new TaskId(1));
        TaskSnapshot done = cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Contains("任务 00000001 已批准，继续下一步。", cli.Output.Output);
    }

    [Fact]
    public void Task_answer_requires_node_path_when_multiple_inputs_wait()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.TwoInputFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Answer(new TaskId(1), null, "内容");
        Assert.Contains("有 2 个输入节点等待回答", cli.ErrorsOut.Output);

        cli.Tasks.Answer(new TaskId(1), "整体/输入二", "乙");
        cli.Tasks.Answer(new TaskId(1), "整体/输入一", "甲");
        cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_answer_keeps_the_leading_node_label_in_text()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Answer(new TaskId(1), null, "用户输入 需要 补充多语言");
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
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Wait(new TaskId(1), _ =>
            cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0
            && cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count > 0);

        cli.Tasks.Approve(new TaskId(1));
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0
            && cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count == 0);

        cli.Tasks.Answer(new TaskId(1), null, "补充背景");
        cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_answer_reports_the_receipt()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Answer(new TaskId(1), null, "补充多语言");

        Assert.Contains("任务 00000001 已收到回答，继续下一步。", cli.Output.Output);
    }

    [Fact]
    public void Task_title_renames_the_task()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");

        cli.Tasks.Rename(new TaskId(1), "文档 补齐");

        Assert.Contains("已改标题。", cli.Output.Output);
        Assert.Equal("文档 补齐", cli.Environment.Snapshot(new TaskId(1)).Title);
    }

    [Fact]
    public void Task_rework_resumes_a_blocked_task()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.BlockingFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));
        Assert.Equal(TaskState.Blocked, cli.Environment.Snapshot(new TaskId(1)).State);

        cli.Tasks.Rework(new TaskId(1));

        Assert.Contains("任务 00000001 已返工。", cli.Output.Output);
    }

    [Fact]
    public void Task_stop_terminates_the_task()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Tasks.StopTask(new TaskId(1));

        Assert.Contains("任务 00000001 已终止。", cli.Output.Output);
        Assert.Equal(TaskState.Terminated, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact]
    public void Task_browser_offers_pause_then_resume()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Tasks.Browse();

        Assert.Contains("暂停任务", cli.Output.Output);

        cli.Tasks.Pause(new TaskId(1));
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.State == TaskState.Paused);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Tasks.Browse();

        Assert.Contains("恢复任务", cli.Output.Output);

        cli.Tasks.StopTask(new TaskId(1));
    }


    [Fact]
    public void Task_stop_execution_cancels_a_single_execution()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.DelayMs = 20000;
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Tasks.StopExecution(new ExecutionId(1));

        Assert.Contains("执行 #1 已取消。", cli.Output.Output);
    }

    [Fact]
    public async Task Task_list_does_not_show_the_dialogue_task()
    {
        using ConsoleSession cli = new();

        var reply = await cli.Environment.Dialogue.ReplyAsync("你好", null);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Tasks.Browse();

        Assert.False(reply.IsError);
        Assert.Contains("还没有任务", cli.Output.Output);
    }

    [Fact]
    public async Task Task_stop_cannot_reach_the_dialogue_task()
    {
        using ConsoleSession cli = new();

        var reply = await cli.Environment.Dialogue.ReplyAsync("你好", null);
        cli.Tasks.StopTask(new TaskId(1));

        Assert.False(reply.IsError);
        Assert.Contains("没有任务 00000001", cli.ErrorsOut.Output);
    }

    [Fact]
    public async Task Assistant_menu_lists_the_actions_and_status()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Contains("当前模型 fake", cli.Output.Output);
        Assert.Contains("回复语言 中文", cli.Output.Output);
        Assert.Contains("→ 发送消息", cli.Output.Output);
        Assert.Contains("√ 切换模型", cli.Output.Output);
        Assert.Contains("- 重置对话", cli.Output.Output);
        Assert.Contains("← 返回主菜单", cli.Output.Output);
    }

    [Fact]
    public async Task Assistant_send_returns_to_the_menu_on_an_empty_line()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Contains("Kuroe >", cli.Output.Output);
        Assert.Equal(2, cli.Output.Output.Split("── 主菜单 › Kuroe 助手").Length - 1);
    }

    [Fact]
    public async Task Assistant_menu_resets_the_dialogue()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Contains("对话已重置。", cli.Output.Output);
    }

    [Fact]
    public async Task Assistant_menu_switches_the_model()
    {
        using ConsoleSession cli = new();
        cli.Environment.Catalog.AddModel(ModelName.Create("m2").Value, ProviderName.Create("test").Value).ThrowIfError();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Equal("m2", cli.Environment.Models.Current?.Value);
        Assert.Contains("已保存并生效。", cli.Output.Output);
    }

    [Fact]
    public async Task Repl_tool_page_returns_to_the_main_menu()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        await cli.Repl.RunAsync();

        string output = cli.Output.Output;
        Assert.Contains("── 主菜单 › 工具", output);
        Assert.Contains("← 返回上一级", output);

        int back = output.IndexOf("← 返回上一级", StringComparison.Ordinal);
        Assert.True(back >= 0 && output.IndexOf("Kuroe 助手", back, StringComparison.Ordinal) > back, output);
    }

    [Fact]
    public async Task Repl_shows_the_main_menu_and_exits()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        await cli.Repl.RunAsync();

        Assert.Contains("主菜单", cli.Output.Output);
        Assert.Contains("Kuroe 助手", cli.Output.Output);
        Assert.Contains("退出程序", cli.Output.Output);
        Assert.DoesNotContain("重置对话", cli.Output.Output);
    }

    [Fact]
    public async Task Repl_exit_returns_to_the_menu_until_confirmed()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        await cli.Repl.RunAsync();

        Assert.Contains("退出程序？未完成的任务会保存为暂停", cli.Output.Output);
        Assert.Contains("← 返回主菜单", cli.Output.Output);
        Assert.Contains("× 确认退出", cli.Output.Output);
    }

    [Fact]
    public async Task Repl_enters_dialogue_by_default_and_returns_on_an_empty_line()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        await cli.Repl.RunAsync();

        Assert.Contains("Kuroe >", cli.Output.Output);
    }

    [Fact]
    public async Task Repl_leaves_the_dialogue_on_escape()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        await cli.Repl.RunAsync();

        Assert.Contains("Kuroe >", cli.Output.Output);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\d{2}:\d{2}:\d{2}")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}