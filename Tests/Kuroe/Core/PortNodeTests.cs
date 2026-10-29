using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输出端口：命名段交回与按端口消费，系统指令进入请求最前的系统指令通道。</summary>
public sealed class PortNodeTests
{
    [Fact]
    public void Port_output_feeds_named_segment_to_downstream()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        ExecutionSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Label.Value == "实施")
            .Executions.Single();
        Assert.Contains(implement.Input.Inputs, value =>
            value.Origin is ExecutionSource { Port: var port }
                && port.Value == "结论"
                && value.Text.Contains("结论产出"));
        Assert.DoesNotContain(implement.Input.Inputs, value => value.Text.Contains("理由产出"));
    }

    [Fact]
    public void Port_output_without_submitted_values_blocks()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.ExecutableStates, state => state.State == NodeState.Blocked);
    }

    [Fact]
    public void Whole_node_reference_loads_as_named_port()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        // 复盘按端口引用取值，出处带端口名
        ExecutionSnapshot review = environment.Runner.Started
            .Single(execution => execution.Input.Node.Label.Value == "复盘")
            .Snapshot();
        Assert.Contains(review.Input.Inputs, value =>
            value.Origin is ExecutionSource { Port: var port } && port.Value == "理由");
    }

    [Fact]
    public void Context_port_injects_upstream_context_keeping_sources()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ContextPortFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        ExecutionSnapshot report = done.Executables
            .Single(entry => entry.Executable.Label.Value == "汇报")
            .Executions.Single();

        // 上游执行的输入取值按原出处透传进下游
        Assert.Contains(report.Input.Inputs, value =>
            value.Origin is ExecutionSource source
                && source.Node == NodePath.Of("整体", "制定")
                && value.Text.Contains("计划产出"));
    }

    [Fact]
    public void Context_input_places_source_output_first()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.ContextInputFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        ExecutionSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Label.Value == "实施")
            .Executions.Single();

        // 上下文输入边与普通 From 各注入一次来源产出，两条取值来源相同
        Assert.Equal(2, implement.Input.Inputs.Count);
        Assert.All(implement.Input.Inputs, value => Assert.Equal("结论产出", value.Text));
    }

    /// <summary>只写 Context 条目、无数据 From 的节点也能启动：来源放行后启动，取值按原出处注入。</summary>
    [Fact]
    public void Context_only_node_starts_after_source_releases()
    {
        const string flow = """
            {
              "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
                { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                  { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Context" }], "From": [ { "结论": "准备@结论" } ] }
                ] }
              ] } ]
            }
            """;

        using TestEnvironment environment = TestEnvironment.Create(flow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        ExecutionSnapshot implement = done.Executables
            .Single(entry => entry.Executable.Label.Value == "实施")
            .Executions.Single();
        Assert.Contains(implement.Input.Inputs, value => value.Text.Contains("结论产出"));
    }

    [Fact]
    public void System_prompt_comes_from_the_declared_blocks()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SystemPromptFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        ExecutionSnapshot execution = done.Executables.Single().Executions.Single();

        // 系统指令只由节点声明的恒定块组成
        Assert.Equal("固定背景一\n固定背景二", Assert.IsType<LlmNodeType>(execution.Input.Type).SystemPrompt());
    }

    [Fact]
    public void Contract_tool_description_lists_declared_ports()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        IExecution ported = environment.Runner.Started.Single(execution => execution.Input.OutputPorts.Count > 0);
        ToolFunction declared = Assert.Single(environment.PortToolFor(ported.Id).Functions);
        Assert.Contains("本节点需要交回的端口：结论、理由", declared.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submitted_port_values_cannot_be_submitted_twice()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.DoubleSubmitPorts = true;

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Runner.Submissions, text => text.Contains("无需重复提交", StringComparison.Ordinal));
    }

    [Fact]
    public void Port_submission_with_at_in_key_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow);
        environment.Runner.PortValuesJson = """{ "结论@甲": "结论产出", "理由": "理由产出" }""";

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        Assert.Contains(environment.Runner.Submissions, text => text.Contains("端口名不能为空或含 @"));
    }

    /// <summary>Signal 端口交回端口名即收口，空值不算缺，本轮产出按声明齐备。</summary>
    [Fact]
    public void Signal_port_settles_the_round_on_its_name_alone()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SignalPortFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Contains(environment.Runner.Submissions,
            text => text.Contains("已记录 1 个端口的产出。本轮产出已齐备。", StringComparison.Ordinal));
    }

    /// <summary>Signal 端口的取值不承载内容，下游取不到它。</summary>
    [Fact]
    public void Signal_port_content_stays_out_of_downstream_inputs()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SignalPortFlow);
        environment.Runner.PortValuesJsonFor = execution =>
            execution.Input.Node.Label.Value == "动手" ? """{ "完成": "本轮标记" }""" : null;

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        ExecutionSnapshot finish = done.Executables
            .Single(entry => entry.Executable.Label.Value == "收尾")
            .Executions.Single();
        Assert.Empty(finish.Input.Inputs);
    }

    /// <summary>Signal 端口未交回时本轮不收口，节点停在阻塞。</summary>
    [Fact]
    public void Signal_port_without_its_submission_blocks()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SignalPortFlow);
        environment.Runner.SubmitsPorts = false;

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.ExecutableStates, state => state.State == NodeState.Blocked);
    }

    /// <summary>端口交回工具的说明按需交回端口列出 Signal 端口。</summary>
    [Fact]
    public void Contract_tool_description_lists_the_signal_port()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.SignalPortFlow);

        TaskId id = environment.Submit("目标");
        environment.Settle(id);

        IExecution moved = environment.Runner.Started.Single(execution => execution.Input.Node.Label.Value == "动手");
        ToolFunction declared = Assert.Single(environment.PortToolFor(moved.Id).Functions);

        Assert.Contains("本节点需要交回的端口：完成", declared.Definition.Description, StringComparison.Ordinal);
    }
}
