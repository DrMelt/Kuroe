using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Nodes;

/// <summary>触发端口：交回触发值，启动组按值决定是否启动；不承载触发的端口不能给出触发位。</summary>
public sealed class TriggerPortTests
{
    /// <summary>TextTrigger 交回触发位：false 消耗组不启动，true 文本进入下游并启动。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Text_trigger_value_decides_whether_downstream_starts(bool trigger)
    {
        using TestEnvironment environment = TestEnvironment.Create(TextTriggerFlow);
        string flag = trigger ? "true" : "false";
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? $$"""{ "结论": { "text": "继续", "trigger": {{flag}} } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = trigger
            ? environment.Settle(id)
            : environment.Wait(id, current => current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.Equal(TaskState.Done, snapshot.State);
        if (trigger)
        {
            GraphExecutionSnapshot downstream = Assert.Single(environment.GraphExecutions(snapshot, "下游"));
            Assert.Contains(downstream.Input.Inputs, value => value.Text == "继续");
            return;
        }

        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
        Assert.Contains(environment.Runner.Submissions, text =>
            text.Contains("其中 1 个端口不触发下游", StringComparison.Ordinal));
        Assert.Contains(snapshot.Journal, entry =>
            entry.Level == JournalLevel.Information
            && entry.Text.Contains("下游", StringComparison.Ordinal)
            && entry.Text.Contains("来源@结论", StringComparison.Ordinal));
    }

    /// <summary>翻转门控：来源触发值为 false 时下游启动，true 时抑制并消耗组。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flipped_trigger_binding_admits_only_a_false_source_value(bool trigger)
    {
        using TestEnvironment environment = TestEnvironment.Create(FlippedTriggerFlow);
        string flag = trigger ? "true" : "false";
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? $$"""{ "结论": { "text": "继续", "trigger": {{flag}} } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = trigger
            ? environment.Wait(id, current => current.State == TaskState.Done && HasTriggerInfo(current))
            : environment.Settle(id);

        Assert.Equal(TaskState.Done, snapshot.State);
        if (trigger)
        {
            Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
            Assert.Contains(snapshot.Journal, entry =>
                entry.Level == JournalLevel.Information
                && entry.Text.Contains("翻转条件要求 false", StringComparison.Ordinal));
            return;
        }

        GraphExecutionSnapshot downstream = Assert.Single(environment.GraphExecutions(snapshot, "下游"));
        Assert.Contains(downstream.Input.Inputs, value => value.Text == "继续");
    }

    /// <summary>检查结论分别门控返工与提交：false 启动返工，true 启动提交。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Verdict_gates_rework_and_commit_on_opposite_values(bool passed)
    {
        using TestEnvironment environment = TestEnvironment.Create(VerdictGateFlow);
        string verdict = passed ? "true" : "false";
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "检查" => $$"""{"结论": {"trigger": {{verdict}}}, "报告": "问题", "提交说明": "本轮未通过"}""",
            _ => """{"交付": "完成"}""",
        };

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current => current.State == TaskState.Done);

        if (passed)
        {
            Assert.Empty(environment.GraphExecutions(snapshot, "执行"));
            Assert.Single(environment.GraphExecutions(snapshot, "暂存"));
            return;
        }

        Assert.Single(environment.GraphExecutions(snapshot, "执行"));
        Assert.Empty(environment.GraphExecutions(snapshot, "暂存"));
    }

    /// <summary>SignalTrigger 为 false 时下游组被消耗但不启动，信号端口不承载内容。</summary>
    [Fact]
    public void SignalTrigger_false_consumes_group_without_starting_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(SignalTriggerFlow);
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? """{ "信号": { "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
    }

    /// <summary>同一来源的另一个端口不受影响：停用的端口只消费自己的组。</summary>
    [Fact]
    public void Silent_port_does_not_block_another_port_of_the_same_source()
    {
        using TestEnvironment environment = TestEnvironment.Create(TwoPortFlow);
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? """{ "停": { "text": "停", "trigger": false }, "走": "走" }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done
            && environment.GraphExecutions(current, "走下游").Count == 1
            && HasTriggerInfo(current));

        var item = Assert.Single(environment.GraphExecutions(snapshot, "走下游"));
        Assert.Contains(item.Input.Inputs, value => value.Text == "走");
        Assert.Empty(environment.GraphExecutions(snapshot, "停下游"));
    }

    /// <summary>普通 Text 端口不接受触发值，交回被拒后节点停在阻塞。</summary>
    [Fact]
    public void Plain_port_rejects_a_trigger_value()
    {
        using TestEnvironment environment = TestEnvironment.Create(PlainTextFlow);
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? """{ "结论": { "text": "试图停用", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Wait(id, current => current.State == TaskState.Blocked);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Contains(environment.Runner.Submissions, text =>
            text.Contains("普通端口只接受文本", StringComparison.Ordinal));
    }

    /// <summary>容器输出端口转发成员端口的触发值，false 时下游组被消耗。</summary>
    [Fact]
    public void Container_forwards_member_trigger_value()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerTriggerFlow);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "成员"
            ? """{ "结论": { "text": "箱内停用", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
        Assert.Contains(snapshot.Journal, entry =>
            entry.Level == JournalLevel.Information
            && entry.Text.Contains("箱@结论", StringComparison.Ordinal));
    }

    /// <summary>审批节点收到 false 时不进入待批准，下游不启动。</summary>
    [Fact]
    public void Approval_does_not_wait_when_the_source_does_not_trigger()
    {
        using TestEnvironment environment = TestEnvironment.Create(ApprovalTriggerFlow);
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? """{ "结论": { "text": "无需批准", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.False(environment.Awaiting(id));
        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
    }

    /// <summary>抑制发生时另一分支仍在执行，任务保持进行；该分支完成后任务收口。</summary>
    [Fact]
    public async Task Suppressed_branch_settles_after_the_other_branch_completes()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelTriggerFlow);
        NodePath slow = NodePath.Of("整体", "慢来源");
        environment.Runner.Gate.Hold(slow);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "快来源"
            ? """{ "停": { "text": "停", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot running = await environment.WaitAsync(id, current =>
            HasTriggerInfo(current) && current.LiveExecutions > 0);

        Assert.Equal(TaskState.Running, running.State);

        environment.Runner.Gate.Open(slow);

        TaskSnapshot done = await environment.SettleAsync(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(environment.GraphExecutions(done, "慢下游"));
        Assert.Empty(environment.GraphExecutions(done, "快下游"));
    }

    /// <summary>普通 Text 端口配置触发后可以交回 false，下游组被消耗但不启动。</summary>
    [Fact]
    public void Text_port_with_trigger_configuration_can_silence_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(TextConfiguredTriggerFlow);
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? """{ "结论": { "text": "暂不触发", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
    }

    /// <summary>容器输出不承载触发时忽略成员的 false，下游照常启动。</summary>
    [Fact]
    public void Container_output_without_trigger_configuration_ignores_member_false()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerPlainOutputFlow);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "成员"
            ? """{ "结论": { "text": "箱内停用", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        GraphExecutionSnapshot downstream = Assert.Single(environment.GraphExecutions(done, "下游"));
        Assert.Contains(downstream.Input.Inputs, value => value.Text == "箱内停用");
    }

    /// <summary>容器输出配置触发时转发成员的 false，下游组被消耗但不启动。</summary>
    [Fact]
    public void Container_output_with_trigger_configuration_forwards_member_false()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerTriggeredOutputFlow);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "成员"
            ? """{ "结论": { "text": "箱内停用", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
    }

    /// <summary>容器触发输入为 false 时容器不发布，下游不启动；为 true 时容器发布并按自身输出配置转发成员触发值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Container_input_trigger_gates_container_publication(bool trigger)
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerInputTriggerFlow);
        string flag = trigger ? "true" : "false";
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "来源" => $$"""{ "结论": { "text": "继续", "trigger": {{flag}} } }""",
            "实施" => """{ "结论": "箱内内容" }""",
            _ => null,
        };

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current => current.State == TaskState.Done);

        if (trigger)
        {
            GraphExecutionSnapshot downstream = Assert.Single(environment.GraphExecutions(snapshot, "下游"));
            Assert.Contains(downstream.Input.Inputs, value => value.Text == "箱内内容");
            return;
        }

        Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
        Assert.Contains(snapshot.Journal, entry =>
            entry.Level == JournalLevel.Information
            && entry.Text.Contains("整体", StringComparison.Ordinal)
            && entry.Text.Contains("来源@结论", StringComparison.Ordinal));
    }

    /// <summary>容器触发输入翻转门控：来源触发值为 false 时容器发布，true 时抑制发布。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flipped_container_input_trigger_admits_only_a_false_source_value(bool trigger)
    {
        using TestEnvironment environment = TestEnvironment.Create(FlippedContainerInputFlow);
        string flag = trigger ? "true" : "false";
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "来源" => $$"""{ "结论": { "text": "继续", "trigger": {{flag}} } }""",
            "实施" => """{ "结论": "箱内内容" }""",
            _ => null,
        };

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = trigger
            ? environment.Wait(id, current => current.State == TaskState.Done && HasTriggerInfo(current))
            : environment.Settle(id);

        Assert.Equal(TaskState.Done, snapshot.State);
        if (trigger)
        {
            Assert.Empty(environment.GraphExecutions(snapshot, "下游"));
            Assert.Contains(snapshot.Journal, entry =>
                entry.Level == JournalLevel.Information
                && entry.Text.Contains("翻转条件要求 false", StringComparison.Ordinal));
            return;
        }

        GraphExecutionSnapshot downstream = Assert.Single(environment.GraphExecutions(snapshot, "下游"));
        Assert.Contains(downstream.Input.Inputs, value => value.Text == "箱内内容");
    }

    /// <summary>Signal 门控只门控容器发布，成员第二次发布仍驱动容器发布并放下游。</summary>
    [Fact]
    public void Signal_gate_does_not_mask_member_second_publish()
    {
        using TestEnvironment environment = TestEnvironment.Create(SignalGateContainerFlow);
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "来源" => """{ "内容": "启动", "信号": { "trigger": true } }""",
            "加工" => """{ "结论": "结果" }""",
            _ => null,
        };

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(2, environment.GraphExecutions(done, "加工").Count);
        Assert.Equal(2, environment.GraphExecutions(done, "下游").Count);
    }

    /// <summary>本轮过程记录里是否已有触发抑制的信息条目。</summary>
    private static bool HasTriggerInfo(TaskSnapshot snapshot) =>
        snapshot.Journal.Any(entry =>
            entry.Level == JournalLevel.Information
            && entry.Text.Contains("未满足触发条件", StringComparison.Ordinal));

    /// <summary>是否为来源节点，用于区分交回值。</summary>
    private static bool SourceValues(IGraphExecution execution) =>
        execution.Input.Node.Label.Value is "来源" or "成员";

    /// <summary>一条带翻转门控的流程：下游输入端口声明 Trigger，绑定条件为 Negative。</summary>
    private const string FlippedTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": { "Ref": "来源@结论", "Condition": "Negative" } } ] }
          ] } } ]
        }
        """;

    private static readonly string ContainerInputTriggerFlow = ContainerInputFlow("\"来源@结论\"");

    /// <summary>与 ContainerInputTriggerFlow 相同，容器触发输入改用翻转门控。</summary>
    private static readonly string FlippedContainerInputFlow =
        ContainerInputFlow("{ \"Ref\": \"来源@结论\", \"Condition\": \"Negative\" }");

    /// <summary>容器触发输入的流程：把关连接取给定的来源值。</summary>
    private static string ContainerInputFlow(string gate) => $$"""
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "测试组", "Kind": "Container", "Inputs": [{ "Name": "把关", "Type": "Text", "Mode": "Trigger" }, { "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "把关": {{gate}}, "目标": "@目标" } ], "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger", "Ref": "实施@结论" }], "Nodes": [
              { "Name": "实施", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
            ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "测试组@结论" } ] }
          ] } } ]
        }
        """;

    /// <summary>外壳内一个 Signal 门控容器，成员自环执行两次，下游消费容器输出。</summary>
    private const string SignalGateContainerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "外壳", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "内容", "Type": "Text" }, { "Name": "信号", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "把关", "Type": "Signal", "Mode": "Trigger" }, { "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "把关": "来源@信号", "目标": "来源@内容" } ], "Outputs": [{ "Name": "结论", "Type": "Text", "Ref": "加工@结论" }], "Nodes": [
              { "Name": "加工", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 2, "Inputs": [{ "Name": "反馈", "Type": "Text" }], "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "反馈": "@目标" }, { "反馈": "加工@结论" } ] }
            ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "整体@结论" } ] }
          ] } } ]
        }
        """;

    private const string TextTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "来源@结论" } ] }
          ] } } ]
        }
        """;

    private const string SignalTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "信号", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "信号", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "信号": "来源@信号" } ] }
          ] } } ]
        }
        """;

    private const string TwoPortFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }, { "Name": "走", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "停下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }], "From": [ { "停": "来源@停" } ] },
            { "Name": "走下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "走", "Type": "Text" }], "From": [ { "走": "来源@走" } ] }
          ] } } ]
        }
        """;

    private const string ParallelTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "慢来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "慢下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "慢来源@结论" } ] },
            { "Name": "快来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "快下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }], "From": [ { "停": "快来源@停" } ] }
          ] } } ]
        }
        """;

    private const string PlainTextFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
          ] } } ]
        }
        """;

    private const string ContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger", "Ref": "成员@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ], "Nodes": [
              { "Name": "成员", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] }
            ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "箱@结论" } ] }
          ] } } ]
        }
        """;

    private const string TextConfiguredTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "来源@结论" } ] }
          ] } } ]
        }
        """;

    private const string ContainerPlainOutputFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text", "Ref": "成员@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ], "Nodes": [
              { "Name": "成员", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] }
            ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "箱@结论" } ] }
          ] } } ]
        }
        """;

    private const string ContainerTriggeredOutputFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger", "Ref": "成员@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ], "Nodes": [
              { "Name": "成员", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] }
            ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "箱@结论" } ] }
          ] } } ]
        }
        """;

    private const string ApprovalTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "来源", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "来源@结论" } ] },
            { "Name": "下游", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "审阅@结论" } ] }
          ] } } ]
        }
        """;

    private const string VerdictGateFlow = """
        {
          "Flows": [ { "Name": "默认", "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }], "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
            { "Name": "检查", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Signal", "Mode": "Trigger" }, { "Name": "报告", "Type": "Text" }, { "Name": "提交说明", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
            { "Name": "执行", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "检查报告", "Type": "Text" }, { "Name": "检查结论", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "检查报告": "检查@报告", "检查结论": { "Ref": "检查@结论", "Condition": "Negative" } } ], "Outputs": [{ "Name": "交付", "Type": "Text" }] },
            { "Name": "暂存", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "触发", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "触发": "检查@结论" } ], "Outputs": [{ "Name": "交付", "Type": "Text" }] }
          ] } } ]
        }
        """;
}