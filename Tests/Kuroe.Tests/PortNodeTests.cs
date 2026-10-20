using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Tool.Contract;
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
              "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "准备", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                  { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Context" }], "From": [ { "结论": "准备@结论" } ] }
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

        PortTool tool = environment.PortTool;

        // 未绑定执行的载体不写端口名，绑定执行后按该执行的声明列出
        Assert.DoesNotContain("结论", tool.Functions.Single().Definition.Description);
        IExecution ported = environment.Runner.Started.Single(execution => execution.Input.OutputPorts.Count > 0);
        ToolFunction declared = Assert.Single(tool.ForExecution(ported.Id).Functions);
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
}
