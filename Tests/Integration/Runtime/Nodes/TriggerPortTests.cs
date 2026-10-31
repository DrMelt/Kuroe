using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
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
            ExecutionSnapshot downstream = Assert.Single(Executions(snapshot, "下游"));
            Assert.Contains(downstream.Input.Inputs, value => value.Text == "继续");
            return;
        }

        Assert.Empty(Executions(snapshot, "下游"));
        Assert.Contains(environment.Runner.Submissions, text =>
            text.Contains("其中 1 个端口不触发下游", StringComparison.Ordinal));
        Assert.Contains(snapshot.Journal, entry =>
            entry.Level == JournalLevel.Information
            && entry.Text.Contains("下游", StringComparison.Ordinal)
            && entry.Text.Contains("来源@结论", StringComparison.Ordinal));
    }

    /// <summary>SignalTrigger 为 false 时下游组被消耗但不启动，信号端口不承载内容。</summary>
    [Fact]
    public void SignalTrigger_false_consumes_group_without_starting_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(SignalTriggerFlow);
        environment.Runner.PortValuesJsonFor = execution => SourceValues(execution)
            ? """{ "信号": false }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot snapshot = environment.Wait(id, current =>
            current.State == TaskState.Done && HasTriggerInfo(current));

        Assert.Empty(Executions(snapshot, "下游"));
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
            && Executions(current, "走下游").Count == 1
            && HasTriggerInfo(current));

        Assert.Single(Executions(snapshot, "走下游"));
        Assert.Contains(Executions(snapshot, "走下游")[0].Input.Inputs, value => value.Text == "走");
        Assert.Empty(Executions(snapshot, "停下游"));
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
            text.Contains("普通端口不承载触发值", StringComparison.Ordinal));
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

        Assert.Empty(Executions(snapshot, "下游"));
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
        Assert.Empty(Executions(snapshot, "下游"));
    }

    /// <summary>抑制发生时另一分支仍在执行，任务保持进行；该分支完成后任务收口。</summary>
    [Fact]
    public void Suppressed_branch_settles_after_the_other_branch_completes()
    {
        using TestEnvironment environment = TestEnvironment.Create(ParallelTriggerFlow);
        environment.Runner.DelayFor = execution => execution.Input.Node.Label.Value == "慢来源" ? 600 : 0;
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value == "快来源"
            ? """{ "停": { "text": "停", "trigger": false } }"""
            : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot running = environment.Wait(id, current =>
            HasTriggerInfo(current) && current.LiveExecutions > 0);

        Assert.Equal(TaskState.Running, running.State);

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(Executions(done, "慢下游"));
        Assert.Empty(Executions(done, "快下游"));
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

        Assert.Empty(Executions(snapshot, "下游"));
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

        ExecutionSnapshot downstream = Assert.Single(Executions(done, "下游"));
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

        Assert.Empty(Executions(snapshot, "下游"));
    }

    /// <summary>本轮过程记录里是否已有触发抑制的信息条目。</summary>
    private static bool HasTriggerInfo(TaskSnapshot snapshot) =>
        snapshot.Journal.Any(entry =>
            entry.Level == JournalLevel.Information
            && entry.Text.Contains("未触发", StringComparison.Ordinal));

    /// <summary>某个标签的执行节点已启动的执行。</summary>
    private static IReadOnlyList<ExecutionSnapshot> Executions(TaskSnapshot snapshot, string label) =>
        snapshot.Executables.Single(entry => entry.Executable.Label.Value == label).Executions;

    /// <summary>是否为来源节点，用于区分交回值。</summary>
    private static bool SourceValues(IExecution execution) =>
        execution.Input.Node.Label.Value is "来源" or "成员";

    private const string TextTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "来源@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string SignalTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "信号", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "信号", "Type": "Signal", "Mode": "Trigger" }], "From": [ { "信号": "来源@信号" } ] }
            ] }
          ] } ]
        }
        """;

    private const string TwoPortFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }, { "Name": "走", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "停下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }], "From": [ { "停": "来源@停" } ] },
              { "Name": "走下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "走", "Type": "Text" }], "From": [ { "走": "来源@走" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ParallelTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "慢来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "慢下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "慢来源@结论" } ] },
              { "Name": "快来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "快下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "停", "Type": "Text", "Mode": "Trigger" }], "From": [ { "停": "快来源@停" } ] }
            ] }
          ] } ]
        }
        """;

    private const string PlainTextFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger", "Ref": "成员@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                { "Name": "成员", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] }
              ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "箱@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string TextConfiguredTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "来源@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ContainerPlainOutputFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text", "Ref": "成员@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                { "Name": "成员", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] }
              ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "箱@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ContainerTriggeredOutputFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger", "Ref": "成员@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                { "Name": "成员", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] }
              ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "箱@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ApprovalTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "来源@结论" } ] },
              { "Name": "下游", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }], "From": [ { "结论": "审阅@结论" } ] }
            ] }
          ] } ]
        }
        """;
}