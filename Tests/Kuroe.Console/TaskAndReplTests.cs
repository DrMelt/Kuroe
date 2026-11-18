using ApiHub.Shared.Models;
using Kuroe.Assistant.Dialogue;
using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Model;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Pending;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>任务浏览与终端主循环。</summary>
public sealed partial class TaskAndReplTests
{
    [Fact(DisplayName = "提交任务报告该任务")]
    public void Task_submit_reports_the_task()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");

        Assert.Contains("已提交 任务 00000001", cli.Output.Output);
        Assert.Single(cli.Environment.TaskReader.Snapshots());
    }

    [Fact(DisplayName = "空目标提交被拒绝")]
    public void Task_submit_with_a_blank_goal_is_rejected()
    {
        using ConsoleSession cli = new();

        cli.Tasks.Submit(new FlowName("默认"), "   ");

        Assert.Contains("开始端口 目标 的输入不能为空。", cli.ErrorsOut.Output);
        Assert.Empty(cli.Environment.TaskReader.Snapshots());
    }

    [Fact(DisplayName = "提交任务询问每个开始端口")]
    public void Task_submit_asks_every_start_port()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.MultiStartFlow);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushText("readme");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushText("plain");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
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

    [Fact(DisplayName = "提交任务把固定开始端口作为上下文显示")]
    public void Task_submit_shows_fixed_start_ports_as_context()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.FixedStartFlow);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushText("readme");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
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

    [Fact(DisplayName = "提交在确认处取消")]
    public void Task_submit_is_cancelled_at_the_confirmation()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.MultiStartFlow);

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushText("readme");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushText("plain");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.End);
        cli.Output.Input.PushKey(ConsoleKey.Enter);

        cli.Tasks.Browse();

        Assert.Empty(cli.Environment.TaskReader.Snapshots());
    }

    [Fact(DisplayName = "任务概览打印执行")]
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

    [Fact(DisplayName = "任务概览打印待处理节点但不打印树")]
    public void Task_overview_prints_pending_nodes_without_the_tree()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count == 1);

        cli.Overview.Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("待批准", cli.Output.Output);
        Assert.Contains("确认计划", cli.Output.Output);
        Assert.DoesNotContain("├", cli.Output.Output);
    }

    [Fact(DisplayName = "任务概览打印阻塞原因")]
    public void Task_overview_prints_the_blocked_reason()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.BlockingFlow);
        cli.Environment.Runner.SubmitsPorts = false;
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        TaskSnapshot blocked = cli.Environment.Settle(new TaskId(1));
        Assert.Equal(TaskState.Blocked, blocked.State);

        cli.Overview.Print(blocked);

        Assert.Contains("计划", cli.Output.Output);
        Assert.Contains("输出端口内容未交回", cli.Output.Output);
    }

    [Fact(DisplayName = "任务过程打印过程记录")]
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

    [Fact(DisplayName = "任务树作为页面打开")]
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

    [Fact(DisplayName = "任务树打印树并标记等待节点")]
    public void Task_tree_prints_a_tree_and_marks_waiting_nodes()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count == 1);

        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        new TaskTreeView(terminal, cli.Environment.Pendings).Print(cli.Environment.Snapshot(new TaskId(1)));

        Assert.Contains("├", output.Output);
        Assert.Contains("└", output.Output);
        Assert.Contains("待批准：确认计划", output.Output);
    }

    [Fact(DisplayName = "任务浏览器打开概览时不展开树")]
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

    [Fact(DisplayName = "任务浏览器标记任务与执行层级")]
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

    [Fact(DisplayName = "停止确认未接受时任务浏览器保留任务")]
    public void Task_browser_keeps_the_task_when_the_stop_confirmation_is_not_accepted()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.Gate.HoldAll();
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

    [Fact(DisplayName = "确认后任务浏览器停止任务")]
    public void Task_browser_stops_the_task_after_confirmation()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.Gate.HoldAll();
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

    [Fact(DisplayName = "任务浏览器列出等待输入并回答选中节点")]
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
        cli.Output.Input.PushText("reply");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
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

    [Fact(DisplayName = "任务浏览器从菜单提交任务")]
    public void Task_browser_submits_a_task_from_the_menu()
    {
        using ConsoleSession cli = new();

        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushText("menu-submit");
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushTextWithEnter(MultilinePrompt.Terminator);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        cli.Tasks.Browse();

        Assert.Contains("已提交 任务 00000001", cli.Output.Output);
        Assert.Equal("menu-submit", Assert.Single(cli.Environment.TaskReader.Snapshots()).Title);
    }

    [Fact(DisplayName = "任务列表显示创建时间不显示进度")]
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

    [Fact(DisplayName = "任务列表显示等待节点数")]
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

    [Fact(DisplayName = "任务执行渲染输入来源与过程")]
    public void Task_execution_renders_input_sources_and_process()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));

        Execution.Shared.State.ExecutionSnapshot execution = cli.Environment.TaskReader.FindExecution(
            new Execution.Shared.Identity.ExecutionId(1)).ThrowIfError();
        cli.Tasks.ShowExecution(execution.Id.Value);

        Assert.Contains("执行 #1", cli.Output.Output);
        Assert.Contains("输入取值", cli.Output.Output);
        Assert.Contains("流程起点端口「目标」", cli.Output.Output);
        Assert.Contains("来源", cli.Output.Output);
        Assert.Contains("过程记录", cli.Output.Output);
        Assert.Contains("› 执行 #1 ", cli.Output.Output);
    }

    [Fact(DisplayName = "没有待批准时批准被拒绝")]
    public void Task_approve_without_awaiting_approval_rejects()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");

        cli.Tasks.Approve(new TaskId(1));

        Assert.Contains("没有等待批准的审批节点", cli.ErrorsOut.Output);
        Assert.Contains("未生效。", cli.ErrorsOut.Output);
    }

    [Fact(DisplayName = "清理丢弃已收口任务")]
    public void Task_clear_drops_settled_tasks()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        cli.Environment.Settle(new TaskId(1));

        cli.Tasks.Clear();

        Assert.Contains("已丢掉 1 个任务。", cli.Output.Output);
        Assert.Empty(cli.Environment.TaskReader.Snapshots());
    }

    [Fact(DisplayName = "批准完成评审任务")]
    public void Task_approve_completes_a_review_task()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.ReviewFlow);
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        TaskSnapshot parked = cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve).Count == 1);
        Assert.Single(cli.Environment.Awaiting(new TaskId(1), PendingKind.Approve));

        cli.Tasks.Approve(new TaskId(1));
        TaskSnapshot done = cli.Environment.Settle(new TaskId(1));

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(TaskState.Running, parked.State);
        Assert.Contains("任务 00000001 已批准，继续下一步。", cli.Output.Output);
    }

    [Fact(DisplayName = "多个输入等待时回答需要节点路径")]
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

    [Fact(DisplayName = "回答文本保留开头节点标签")]
    public void Task_answer_keeps_the_leading_node_label_in_text()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Answer(new TaskId(1), null, "用户输入 需要 补充多语言");
        cli.Environment.Settle(new TaskId(1));

        Assert.Contains(
            cli.Environment.Snapshot(new TaskId(1)).Executions.SelectMany(execution => execution.Request.Inputs),
            value => value.Text.Contains("用户输入 需要 补充多语言"));
    }

    [Fact(DisplayName = "并行等待时批准与回答可并行工作")]
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

    [Fact(DisplayName = "回答报告回执")]
    public void Task_answer_reports_the_receipt()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.InputFlow);
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");
        cli.Environment.Wait(new TaskId(1), _ => cli.Environment.Awaiting(new TaskId(1), PendingKind.Answer).Count > 0);

        cli.Tasks.Answer(new TaskId(1), null, "补充多语言");

        Assert.Contains("任务 00000001 已收到回答，继续下一步。", cli.Output.Output);
    }

    [Fact(DisplayName = "标题重命名任务")]
    public void Task_title_renames_the_task()
    {
        using ConsoleSession cli = new();
        cli.Tasks.Submit(new FlowName("默认"), "补齐 README");

        cli.Tasks.Rename(new TaskId(1), "文档 补齐");

        Assert.Contains("已改标题。", cli.Output.Output);
        Assert.Equal("文档 补齐", cli.Environment.Snapshot(new TaskId(1)).Title);
    }

    [Fact(DisplayName = "重跑恢复阻塞任务")]
    public void Task_rerun_resumes_a_blocked_task()
    {
        using ConsoleSession cli = new(flowsJson: TestFlows.BlockingFlow);
        cli.Environment.Runner.SubmitsPorts = false;
        cli.Tasks.Submit(new FlowName("默认"), "目标");
        cli.Environment.Settle(new TaskId(1));
        Assert.Equal(TaskState.Blocked, cli.Environment.Snapshot(new TaskId(1)).State);

        cli.Tasks.Rerun(new TaskId(1));

        Assert.Contains("任务 00000001 已重新执行。", cli.Output.Output);
    }

    [Fact(DisplayName = "停止终止任务")]
    public void Task_stop_terminates_the_task()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.Gate.HoldAll();
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Tasks.StopTask(new TaskId(1));

        Assert.Contains("任务 00000001 已终止。", cli.Output.Output);
        Assert.Equal(TaskState.Terminated, cli.Environment.Snapshot(new TaskId(1)).State);
    }

    [Fact(DisplayName = "任务浏览器提供暂停后恢复")]
    public void Task_browser_offers_pause_then_resume()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.Gate.HoldAll();
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


    [Fact(DisplayName = "停止执行只停止单个执行")]
    public void Task_stop_execution_stops_a_single_execution()
    {
        using ConsoleSession cli = new();
        cli.Environment.Runner.Gate.HoldAll();
        cli.Tasks.Submit(new FlowName("默认"), "长任务");
        cli.Environment.Wait(new TaskId(1), snapshot => snapshot.LiveExecutions > 0);

        cli.Tasks.StopExecution(1);

        Assert.Contains("执行 #1 已停止。", cli.Output.Output);
    }

    [Fact(DisplayName = "任务列表不显示对话任务")]
    public async Task Task_list_does_not_show_the_dialogue_task()
    {
        using ConsoleSession cli = new();

        var reply = await cli.Environment.SayAsync("你好", null);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Tasks.Browse();

        Assert.False(reply.IsError);
        Assert.Contains("还没有任务", cli.Output.Output);
    }

    [Fact(DisplayName = "停止不能作用于对话任务")]
    public async Task Task_stop_cannot_reach_the_dialogue_task()
    {
        using ConsoleSession cli = new();

        var reply = await cli.Environment.SayAsync("你好", null);
        cli.Tasks.StopTask(new TaskId(1));

        Assert.False(reply.IsError);
        Assert.Contains("没有任务 00000001", cli.ErrorsOut.Output);
    }

    [Fact(DisplayName = "助手菜单列出操作与状态")]
    public async Task Assistant_menu_lists_the_actions_and_status()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Contains("当前模型 fake", cli.Output.Output);
        Assert.Contains("回复语言 中文", cli.Output.Output);
        Assert.Contains("模式 只读", cli.Output.Output);
        Assert.Contains("→ 发送消息", cli.Output.Output);
        Assert.Contains("√ 切换模型", cli.Output.Output);
        Assert.Contains("↔ 切换模式", cli.Output.Output);
        Assert.DoesNotContain("重置对话", cli.Output.Output);
        Assert.Contains("← 返回主菜单", cli.Output.Output);
    }

    [Fact(DisplayName = "助手会话列表不提供全局模型切换")]
    public async Task Assistant_session_list_does_not_offer_the_global_model_switch()
    {
        using ConsoleSession cli = new();
        for (int i = 0; i < 6; i++)
        {
            cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        }

        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Contains("── 主菜单 › Kuroe 助手 › 会话列表", cli.Output.Output);
        Assert.Contains("+ 新建会话", cli.Output.Output);
        Assert.DoesNotContain("切换全局模型", cli.Output.Output);
    }

    [Fact(DisplayName = "助手发送在空行时回到菜单")]
    public async Task Assistant_send_returns_to_the_menu_on_an_empty_line()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Equal(1, cli.Output.Output.Split("Kuroe >").Length - 1);
        Assert.Contains("助手操作", cli.Output.Output);
    }

    [Fact(DisplayName = "助手菜单切换模式")]
    public async Task Assistant_menu_switches_the_mode()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Equal(DialogueMode.Edit, cli.Environment.Session.Mode);
        Assert.Contains("模式已切换为更改，下一轮生效。", cli.Output.Output);
    }

    [Fact(DisplayName = "助手菜单切换模型")]
    public async Task Assistant_menu_switches_the_model()
    {
        using ConsoleSession cli = new();
        cli.Environment.Catalog.AddModel(
            ModelName.Create("m2").Value,
            ProviderName.Create("test").Value,
            ProviderModelName.Create("m2").Value).ThrowIfError();
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.Escape);

        await cli.Assistant.BrowseAsync();

        Assert.Equal("m2", cli.Environment.Models.Current?.Value);
        Assert.Contains("已保存并生效。", cli.Output.Output);
    }

    [Fact(DisplayName = "REPL 工具页回到主菜单")]
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

    [Fact(DisplayName = "REPL 显示主菜单并退出")]
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
    }

    [Fact(DisplayName = "REPL 退出确认前回到菜单")]
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

    [Fact(DisplayName = "REPL 默认进入对话并在空行返回")]
    public async Task Repl_enters_dialogue_by_default_and_returns_on_an_empty_line()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
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

    [Fact(DisplayName = "REPL 按返回键离开对话")]
    public async Task Repl_leaves_the_dialogue_on_escape()
    {
        using ConsoleSession cli = new();
        cli.Output.Input.PushKey(ConsoleKey.Enter);
        cli.Output.Input.PushKey(ConsoleKey.DownArrow);
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