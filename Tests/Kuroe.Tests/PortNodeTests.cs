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
    public void Whole_node_reference_loads_as_named_port()
    {
        using KuroeHarness harness = KuroeHarness.Create(TestFlows.PortFlow);

        TaskId id = harness.Submit("目标");
        harness.Settle(id);

        // 复盘不带端口引用在解析层拒绝；若按端口引用则出处带端口标签
        RunSnapshot review = harness.Executor.Started
            .Single(run => run.Context.NodeIndex == 3 && run.Context.ItemIndex is null)
            .Snapshot();
        Assert.Contains(review.Context.Seed, message => message.Text.Contains("端口「理由」产出"));
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

        // 上游 Seed 里的条目消息原样保留，出处仍可回跳；拆分边不注入规划文本
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

        // 上下文输入边与普通 From 各注入一次命名端口产出，上下文输入那条位于上下文开头
        Assert.Equal(2, implement.Context.Seed.Count(message => message.Text.Contains("端口「结论」产出")));
        Assert.Contains("端口「结论」产出", implement.Context.Seed[0].Text);
    }

    /// <summary>只写 Context 条目、无数据 From 的节点也能启动：来源放行后启动，前缀置于上下文开头。</summary>
    [Fact]
    public void Context_only_node_starts_after_source_releases()
    {
        const string flow = """
            {
              "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
                { "Name": "整体", "Nodes": [
                  { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
                  { "Name": "实施", "Output": "Text", "Model": "执行者", "From": [ { "Node": "准备@结论", "Context": true } ] }
                ] }
              ] } ]
            }
            """;

        using KuroeHarness harness = KuroeHarness.Create(flow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Name.Value == "实施")
            .Runs.Single();
        Assert.Contains("端口「结论」产出", implement.Context.Seed[0].Text);
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

    /// <summary>PerItem 节点的单条目返工只作废该条目，其余实例的端口值保留，下游再次消费不缺内容。</summary>
    [Fact]
    public void Per_item_port_values_survive_single_item_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create("""
            {
              "Flows": [ { "Name": "默认", "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" } ], "Nodes": [
                { "Name": "整体", "Nodes": [
                  { "Name": "制定计划", "Output": "Plan", "Model": "规划者" },
                  { "Name": "实施", "Mode": "PerItem", "Model": "执行者", "Outputs": ["结论"], "From": ["制定计划@拆分"] },
                  { "Name": "汇总", "Model": "执行者", "From": ["实施@结论"] }
                ] }
              ] } ]
            }
            """);
        harness.Executor.ItemsJson = """
            [
              { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
              { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
            ]
            """;
        harness.Executor.FailsWhen = run =>
            run.Context.NodeIndex == 2 && run.Context.ItemIndex == 1 && run.Context.ExecutionCount == 1;
        harness.Executor.PortValuesJsonFor = run =>
        {
            if (run.Context.NodeIndex != 2)
            {
                return null;
            }

            if (run.Context.ItemIndex == 0)
            {
                return """{"结论": "甲产出通过"}""";
            }

            return run.Context.ExecutionCount == 1
                ? """{"结论": "乙产出不合格"}"""
                : """{"结论": "乙产出通过"}""";
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        // 单条目返工只作废乙，甲的交回值保留，汇总上下文不缺甲的产出
        harness.Tasks.Rework(id, 1).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot collect = Assert.Single(done.Executables[2].Runs);
        Assert.Contains(collect.Context.Seed, message => message.Text.Contains("甲产出通过"));
        Assert.Contains(collect.Context.Seed, message => message.Text.Contains("乙产出通过"));
    }

    /// <summary>PerItem 实例收口成功但端口内容未交回时阻塞只落到该实例，指定条目返工重跑该条目不牵连其它实例的端口值。</summary>
    [Fact]
    public void Per_item_missing_port_values_rework_only_that_item()
    {
        using KuroeHarness harness = KuroeHarness.Create("""
            {
              "Flows": [ { "Name": "默认", "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" } ], "Nodes": [
                { "Name": "整体", "Nodes": [
                  { "Name": "制定计划", "Output": "Plan", "Model": "规划者" },
                  { "Name": "实施", "Mode": "PerItem", "Model": "执行者", "Outputs": ["结论"], "From": ["制定计划@拆分"] },
                  { "Name": "汇总", "Model": "执行者", "From": ["实施@结论"] }
                ] }
              ] } ]
            }
            """);
        harness.Executor.ItemsJson = """
            [
              { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
              { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
            ]
            """;
        // 甲的第一轮提交空端口表，端口与声明不一一对应，视为未交回
        harness.Executor.PortValuesJsonFor = run =>
        {
            if (run.Context.NodeIndex != 2)
            {
                return null;
            }

            if (run.Context.ItemIndex == 0 && run.Context.ExecutionCount == 1)
            {
                return "{ }";
            }

            return run.Context.ItemIndex == 0
                ? """{"结论": "甲产出通过"}"""
                : """{"结论": "乙产出通过"}""";
        };

        TaskId id = harness.Submit("补齐 README");
        TaskSnapshot blocked = harness.Settle(id);
        Assert.Equal(TaskState.Blocked, blocked.State);

        // 指定条目返工只重跑甲，乙的交回值保留，汇总上下文同时取到两端口产出
        harness.Tasks.Rework(id, 0).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot collect = Assert.Single(done.Executables[2].Runs);
        Assert.Contains(collect.Context.Seed, message => message.Text.Contains("甲产出通过"));
        Assert.Contains(collect.Context.Seed, message => message.Text.Contains("乙产出通过"));
    }
}
