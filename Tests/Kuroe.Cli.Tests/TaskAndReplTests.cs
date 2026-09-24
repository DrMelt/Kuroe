using Kuroe.Shared.Agent;
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

        Assert.Contains("没有等待批准的步骤", ui.ErrorsOut.Output);
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
}