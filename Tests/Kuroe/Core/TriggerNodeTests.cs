using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Pending;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输入端口类型：Text 输入进上下文供模型取用，Signal 输入不进上下文，只以来源的新版本驱动启动。
/// From 的组是一套完整的输入绑定方案，组内全部齐备该组才成立，组间任一齐备即启动。来源每次发布未消费的新版本触发一次启动。</summary>
public sealed class TriggerNodeTests
{
    /// <summary>纯触发节点无数据输入也能启动：来源发布后触发启动，上下文不含来源产出。</summary>
    [Fact]
    public void Trigger_only_node_starts_and_omits_source_output()
    {
        using TestEnvironment environment = TestEnvironment.Create(TriggerOnceFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Label.Value == "来源").Executions);
        ExecutionSnapshot triggered = done.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions.Single();
        Assert.Empty(triggered.Input.Inputs);
    }

    /// <summary>来源每次回答发布新版本触发一次：两轮回答两次触发，触发目标始终收不到内容。</summary>
    [Fact]
    public void Every_trigger_version_starts_one_execution_without_data()
    {
        using TestEnvironment environment = TestEnvironment.Create(TriggerLoopFlow);

        TaskId id = environment.Submit("对话");
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count > 0);

        environment.Pendings.Answer(id, null, "第一问").ThrowIfError();
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count > 0
            && environment.Runner.Started.Any(execution =>
                execution.Input.Node.Label.Value == "回话" && execution.Input.ExecutionCount == 1));
        ExecutionSnapshot first = environment.Runner.Started
            .Single(execution => execution.Input.Node.Label.Value == "回话" && execution.Input.ExecutionCount == 1)
            .Snapshot();
        Assert.DoesNotContain(first.Input.Inputs, value => value.Text.Contains("第一问"));

        environment.Pendings.Answer(id, null, "第二问").ThrowIfError();
        TaskSnapshot done = environment.Wait(id, snapshot => RoundCount(snapshot, "回话") == 2);
        Assert.Equal(2, RoundCount(done, "回话"));
        ExecutionSnapshot second = environment.Runner.Started
            .Single(execution => execution.Input.Node.Label.Value == "回话" && execution.Input.ExecutionCount == 2)
            .Snapshot();
        Assert.DoesNotContain(second.Input.Inputs, value => value.Text.Contains("第二问"));
    }

    /// <summary>任务里给定标签的执行节点已经启动的执行数。</summary>
    private static int RoundCount(TaskSnapshot snapshot, string label) =>
        snapshot.Executables.Single(node => node.Executable.Label.Value == label).Executions.Count;

    /// <summary>触发与数据 From 并存：数据源产出照常注入，触发源内容不进上下文。</summary>
    [Fact]
    public void Mixed_trigger_and_data_from_keeps_data_only()
    {
        using TestEnvironment environment = TestEnvironment.Create(MixedTriggerFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        ExecutionSnapshot merged = done.Executables.Single(entry => entry.Executable.Label.Value == "合并").Executions.Single();
        Assert.Contains(merged.Input.Inputs, value =>
            value.Origin is ExecutionSource { Node: var node, Port: var port }
                && node == NodePath.Of("整体", "数据")
                && port.Value == "结论");
        Assert.DoesNotContain(merged.Input.Inputs, value =>
            value.Origin is ExecutionSource { Node: var node } && node == NodePath.Of("整体", "信号"));
    }

    /// <summary>容器作触发源：成员齐备放行后目标被触发，容器产出不进目标上下文。</summary>
    [Fact]
    public void Released_container_acts_as_trigger_source()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerTriggerFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        ExecutionSnapshot triggered = done.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions.Single();
        Assert.DoesNotContain(triggered.Input.Inputs, value => value.Text.Contains("准备产出"));
    }

    /// <summary>触发引用可带命名端口：来源放行该端口即触发，端口内容同样不进目标上下文。</summary>
    [Fact]
    public void Named_port_trigger_fires_without_injecting_port_content()
    {
        using TestEnvironment environment = TestEnvironment.Create(NamedPortTriggerFlow);
        environment.Runner.PortValuesJson = """{ "信号": "信号产出" }""";

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        ExecutionSnapshot triggered = done.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions.Single();
        Assert.DoesNotContain(triggered.Input.Inputs, value => value.Text.Contains("信号产出"));
    }

    /// <summary>触发循环受 MaxExecutions 约束：到达上限后来源再发布新版本也不再启动。</summary>
    [Fact]
    public void Trigger_loop_stops_restarting_at_max_executions()
    {
        using TestEnvironment environment = TestEnvironment.Create(TriggerLoopWithLimitFlow);

        TaskId id = environment.Submit("对话");
        environment.Wait(id, _ => environment.Awaiting(id, PendingKind.Answer).Count > 0);

        environment.Pendings.Answer(id, null, "第一问").ThrowIfError();
        environment.Wait(id, _ => environment.Runner.Started.Count == 1 && environment.Awaiting(id, PendingKind.Answer).Count > 0);
        environment.Pendings.Answer(id, null, "第二问").ThrowIfError();
        environment.Wait(id, _ => environment.Runner.Started.Count == 2 && environment.Awaiting(id, PendingKind.Answer).Count > 0);
        environment.Pendings.Answer(id, null, "第三问").ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(entry => entry.Executable.Label.Value == "回话").Executions.Count);
        Assert.Equal([1, 2], [.. environment.Runner.Started.Select(execution => execution.Input.ExecutionCount)]);
    }

    /// <summary>装配层引用库执行节点注入信号条目：引用处的信号接线随实例生效，且库定义本身不写接线。</summary>
    [Fact]
    public void Library_reference_receives_trigger_injection()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryTriggerInjectionFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        ExecutionSnapshot triggered = done.Executables.Single(entry => entry.Executable.Label.Value == "触发实例").Executions.Single();
        Assert.DoesNotContain(triggered.Input.Inputs, value => value.Text.Contains("来源产出"));
    }

    /// <summary>审批节点挡住信号驱动的触发节点：来源发布后停在待批准，批准后触发放行。</summary>
    [Fact]
    public void Approval_gates_a_signal_driven_trigger()
    {
        using TestEnvironment environment = TestEnvironment.Create(TriggerReviewFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot parked = environment.Settle(id);

        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        Assert.Empty(parked.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions);
    }

    /// <summary>触发启动的执行走输出校验通道：首次不通过停在阻塞，返工重跑通过后放行。</summary>
    [Fact]
    public void Trigger_execution_honors_validate_and_rework()
    {
        using TestEnvironment environment = TestEnvironment.Create(TriggerValidateFlow);
        environment.Runner.Reply = execution =>
        {
            if (execution.Input.Node.Label.Value == "来源")
            {
                return "来源产出";
            }

            return execution.Input.ExecutionCount == 1 ? "初稿不够好" : "修订通过";
        };
        environment.Runner.PortValuesJsonFor = execution => execution.Input.Node.Label.Value switch
        {
            "来源" => """{"结论": "来源产出"}""",
            "触发" => execution.Input.ExecutionCount == 1
                ? """{"结论": "初稿不够好"}"""
                : """{"结论": "修订通过"}""",
            _ => null,
        };

        TaskId id = environment.Submit("目标");
        TaskSnapshot blocked = environment.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Node.Label == new NodeLabel("触发")).State);

        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions.Count);
    }
    /// <summary>Review 容器作触发源：成员齐备后审批节点停等批准，批准放行后再触发目标启动。</summary>
    [Fact]
    public void Review_container_trigger_parks_until_approval()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReviewContainerTriggerFlow);

        TaskId id = environment.Submit("目标");
        TaskSnapshot parked = environment.Settle(id);

        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(environment.Awaiting(id, PendingKind.Approve));
        Assert.Empty(parked.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions);

        environment.Pendings.Approve(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Label.Value == "触发").Executions);
    }

    /// <summary>来源先发布再触发下游，触发节点无数据输入。</summary>
    private const string TriggerOnceFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "触发", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "来源@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>对话环：输入节点是挂点，回答每次驱动触发节点按新版本启动一轮，触发目标不接收回答内容。</summary>
    private const string TriggerLoopFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标", "回复": "回话@回复" } ] },
              { "Name": "回话", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "回答", "Type": "Signal" }], "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>数据与触发并行：触发源只发信号，目标只收数据源的产出。</summary>
    private const string MixedTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "数据", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "信号", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "信号", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "合并", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论" }, { "Name": "信号", "Type": "Signal" }], "From": [ { "结论": "数据@结论", "信号": "信号@信号" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>自动容器成员齐备放行后整体作触发源。</summary>
    private const string ContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "准备箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [ { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] } ] },
              { "Name": "触发", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "准备箱@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发引用可带命名端口：来源声明输出端口，端口放行即触发。</summary>
    private const string NamedPortTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "信号", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "触发", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "信号", "Type": "Signal" }], "From": [ { "信号": "来源@信号" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>挂点触发环给出 MaxExecutions 上限：三轮回答只启动两轮，第三问起不再有新执行。</summary>
    private const string TriggerLoopWithLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标", "回复": "回话@回复" } ] },
              { "Name": "回话", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "回答", "Type": "Signal" }], "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ], "MaxExecutions": 2 }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义不写接线，引用处注入信号条目随实例生效。</summary>
    private const string LibraryTriggerInjectionFlow = """
        {
          "Nodes": [ { "Name": "信号工", "Kind": "Llm", "Inputs": [{ "Name": "结论", "Type": "Signal" }] } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Label": "触发实例", "Use": "信号工", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "来源@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>审批节点挡住信号驱动的触发节点，来源发布后停在待批准。</summary>
    private const string TriggerReviewFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "来源@结论" } ] },
              { "Name": "触发", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "审阅@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发节点声明输出校验，执行首轮不通过停在阻塞，返工重跑通过后放行。</summary>
    private const string TriggerValidateFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "来源", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
              { "Name": "触发", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Signal" }], "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "来源@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>审查箱作触发源的载体：成员齐备经审批节点停等批准，批准放行后触发目标启动。</summary>
    private const string ReviewContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "审查箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "干工@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [ { "Name": "干工", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] } ] },
              { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "审查箱@结论" } ] },
              { "Name": "触发", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "审阅@结论" } ] }
            ] }
          ] } ]
        }
        """;
}
