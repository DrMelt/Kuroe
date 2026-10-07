using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Kuroe.Tools;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输出端口：命名段交回与按端口消费，系统指令进入请求最前的系统指令通道。</summary>
public sealed class PortNodeTests
{
    [Fact]
    public void Port_output_feeds_named_segment_to_downstream()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Name.Value == "实施")
            .Runs.Single();
        Assert.Contains(implement.Context.Seed,
            message => message.Text.Contains("节点「分析」的端口「结论」产出")
                && message.Text.Contains("结论产出"));
        Assert.DoesNotContain(implement.Context.Seed, message => message.Text.Contains("理由产出"));
    }

    [Fact]
    public void Port_output_without_submitted_values_blocks()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.SubmitsPorts = false;

        TaskId id = harness.Submit("目标");
        TaskSnapshot blocked = harness.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.ExecutableStates, state => state.State == NodeState.Blocked);
    }

    [Fact]
    public void Whole_node_reference_ignores_ports()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        // 不带端口引用整份取用，出处不带端口标签
        RunSnapshot whole = harness.Executor.Started
            .Single(run => run.Context.NodeIndex == 3 && run.Context.ItemIndex is null)
            .Snapshot();
        Assert.Contains(whole.Context.Seed, message => message.Text.Contains("节点「分析」的产出"));
        Assert.DoesNotContain(whole.Context.Seed, message => message.Text.Contains("端口「"));
    }

    [Fact]
    public void ContextOutput_port_injects_upstream_frame_keeping_sources()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ContextOutputFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        RunSnapshot report = done.Executables
            .Single(entry => entry.Executable.Name.Value == "汇报")
            .Runs.Single();

        // 上游 run 的指令单列，出处标记为上下文帧
        Assert.Contains(report.Context.Seed, message =>
            message.Source is ContextFrameSource { NodeName.Value: "实施" } frame
            && frame.FromRun is not null
            && message.Text.Contains("节点「实施」的指令"));

        // 上游 Seed 里的产出与条目消息原样保留，出处仍可回跳
        Assert.Contains(report.Context.Seed, message =>
            message.Source is RunSource { NodeName.Value: "制定" });
        Assert.Contains(report.Context.Seed, message => message.Source is ItemSource);
    }

    [Fact]
    public void Context_input_places_source_output_first()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.ContextInputFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        RunSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Name.Value == "实施")
            .Runs.Single();

        // 上下文输入边与普通 From 各注入一次整份产出，上下文输入那条位于上下文开头
        Assert.Equal(2, implement.Context.Seed.Count(message => message.Text.Contains("节点「准备」的产出")));
        Assert.Contains("节点「准备」的产出", implement.Context.Seed[0].Text);
    }

    [Fact]
    public void System_prompt_is_assembled_into_run_context()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.SystemPromptFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        RunSnapshot run = done.Executables.Single().Runs.Single();
        Assert.Equal("固定背景一\n固定背景二", run.Context.SystemPrompt);
    }

    [Fact]
    public void Contract_tool_description_lists_declared_ports()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        PortTool tool = new(harness.Executor.PortSubmitter!, harness.Registry);

        // 未绑定回合的载体不写端口名，绑定回合后按该 run 的声明列出
        Assert.DoesNotContain("结论", tool.Functions.Single().Description);
        Run ported = harness.Executor.Started.Single(run => run.Context.OutputPorts.Count > 0);
        ToolFunction declared = Assert.Single(tool.ForTurn(ported.Scope).Functions);
        Assert.Contains("本节点声明端口：结论、理由", declared.Description);
    }

    [Fact]
    public void Port_submission_with_at_in_key_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.PortValuesJson = """{ "结论@甲": "结论产出", "理由": "理由产出" }""";

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        Assert.Contains(harness.Executor.Submissions, text => text.Contains("端口名不能为空或含 @"));
    }

    [Fact]
    public void Duplicate_port_submission_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);
        harness.Executor.DoubleSubmitPorts = true;

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(2, harness.Executor.Submissions.Count);
        Assert.Contains("已记录 2 个端口的产出", harness.Executor.Submissions[0]);
        Assert.Contains("无需重复提交", harness.Executor.Submissions[1]);
        Assert.Equal(TaskState.Done, done.State);
    }
}
