using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.Waiting;
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
        using KuroeHarness harness = KuroeHarness.Create(TriggerOnceFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Name.Value == "来源").Runs);
        RunSnapshot triggered = done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs.Single();
        Assert.Empty(triggered.Context.Instruction);
        Assert.Empty(triggered.Context.Seed);
    }

    /// <summary>来源每次回答发布新版本触发一次：两轮回答两次触发，触发目标始终收不到内容。</summary>
    [Fact]
    public void Every_trigger_version_starts_one_run_without_data()
    {
        using KuroeHarness harness = KuroeHarness.Create(TriggerLoopFlow);

        TaskId id = harness.Submit("对话");
        harness.Wait(id, _ => harness.Awaiting(id, AwaitKind.Answer).Count > 0);

        harness.Waits.Answer(id, null, "第一问").ThrowIfError();
        harness.Wait(id, _ => harness.Awaiting(id, AwaitKind.Answer).Count > 0);
        RunSnapshot first = harness.Executor.Started
            .Single(run => run.Context.NodeName.Value == "回话" && run.Context.ExecutionCount == 1)
            .Snapshot();
        Assert.DoesNotContain(first.Context.Seed, message => message.Text.Contains("第一问"));

        harness.Waits.Answer(id, null, "第二问").ThrowIfError();
        TaskSnapshot done = harness.Wait(id, _ => harness.Awaiting(id, AwaitKind.Answer).Count > 0);
        Assert.Equal(2, done.Executables.Single(node => node.Executable.Name.Value == "回话").Runs.Count);
        RunSnapshot second = harness.Executor.Started
            .Single(run => run.Context.NodeName.Value == "回话" && run.Context.ExecutionCount == 2)
            .Snapshot();
        Assert.DoesNotContain(second.Context.Seed, message => message.Text.Contains("第二问"));
    }

    /// <summary>触发与数据 From 并存：数据源产出照常注入，触发源内容不进上下文。</summary>
    [Fact]
    public void Mixed_trigger_and_data_from_keeps_data_only()
    {
        using KuroeHarness harness = KuroeHarness.Create(MixedTriggerFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        RunSnapshot merged = done.Executables.Single(entry => entry.Executable.Name.Value == "合并").Runs.Single();
        Assert.Contains(merged.Context.Seed, message => message.Text.Contains("节点「数据」的端口「结论」产出"));
        Assert.DoesNotContain(merged.Context.Seed, message => message.Text.Contains("节点「信号」的端口「信号」产出"));
    }

    /// <summary>容器作触发源：成员齐备放行后目标被触发，容器产出不进目标上下文。</summary>
    [Fact]
    public void Released_container_acts_as_trigger_source()
    {
        using KuroeHarness harness = KuroeHarness.Create(ContainerTriggerFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot triggered = done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs.Single();
        Assert.DoesNotContain(triggered.Context.Seed, message => message.Text.Contains("节点「准备」的产出"));
    }

    /// <summary>触发引用可带命名端口：来源放行该端口即触发，端口内容同样不进目标上下文。</summary>
    [Fact]
    public void Named_port_trigger_fires_without_injecting_port_content()
    {
        using KuroeHarness harness = KuroeHarness.Create(NamedPortTriggerFlow);
        harness.Executor.PortValuesJson = """{ "信号": "信号产出" }""";

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot triggered = done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs.Single();
        Assert.DoesNotContain(triggered.Context.Seed, message => message.Text.Contains("节点「来源」"));
        Assert.DoesNotContain(triggered.Context.Seed, message => message.Text.Contains("信号产出"));
    }

    /// <summary>触发循环受 MaxRuns 约束：到达上限后来源再发布新版本也不再启动。</summary>
    [Fact]
    public void Trigger_loop_stops_restarting_at_max_runs()
    {
        using KuroeHarness harness = KuroeHarness.Create(TriggerLoopWithLimitFlow);

        TaskId id = harness.Submit("对话");
        harness.Wait(id, _ => harness.Awaiting(id, AwaitKind.Answer).Count > 0);

        harness.Waits.Answer(id, null, "第一问").ThrowIfError();
        harness.Wait(id, _ => harness.Executor.Started.Count == 1 && harness.Awaiting(id, AwaitKind.Answer).Count > 0);
        harness.Waits.Answer(id, null, "第二问").ThrowIfError();
        harness.Wait(id, _ => harness.Executor.Started.Count == 2 && harness.Awaiting(id, AwaitKind.Answer).Count > 0);
        harness.Waits.Answer(id, null, "第三问").ThrowIfError();

        TaskSnapshot done = harness.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(entry => entry.Executable.Name.Value == "回话").Runs.Count);
        Assert.Equal([1, 2], [.. harness.Executor.Started.Select(run => run.Context.ExecutionCount)]);
    }

    /// <summary>装配层引用库执行节点注入信号条目：引用处的信号接线随实例生效，且库定义本身不写接线。</summary>
    [Fact]
    public void Library_reference_receives_trigger_injection()
    {
        using KuroeHarness harness = KuroeHarness.Create(LibraryTriggerInjectionFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        RunSnapshot triggered = done.Executables.Single(entry => entry.Executable.Name.Value == "触发实例").Runs.Single();
        Assert.DoesNotContain(triggered.Context.Seed, message => message.Text.Contains("节点「来源」的产出"));
    }

    /// <summary>审批节点挡住信号驱动的触发节点：来源发布后停在待批准，批准后触发放行。</summary>
    [Fact]
    public void Approval_gates_a_signal_driven_trigger()
    {
        using KuroeHarness harness = KuroeHarness.Create(TriggerReviewFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot parked = harness.Settle(id);

        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(harness.Awaiting(id, AwaitKind.Approve));
        Assert.Empty(parked.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs);

        harness.Waits.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs);
    }

    /// <summary>触发启动的 run 走输出校验通道：首次不通过停在阻塞，返工重跑通过后放行。</summary>
    [Fact]
    public void Trigger_run_honors_validate_and_rework()
    {
        using KuroeHarness harness = KuroeHarness.Create(TriggerValidateFlow);
        harness.Executor.Output = run =>
        {
            if (run.Context.NodeIndex == 2)
            {
                return "来源产出";
            }

            return run.Context.ExecutionCount == 1 ? "初稿不够好" : "修订通过";
        };
        harness.Executor.PortValuesJsonFor = run => run.Context.NodeIndex switch
        {
            2 => """{"结论": "来源产出"}""",
            3 => run.Context.ExecutionCount == 1
                ? """{"结论": "初稿不够好"}"""
                : """{"结论": "修订通过"}""",
            _ => null,
        };

        TaskId id = harness.Submit("目标");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 3).State);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs.Count);
    }
    /// <summary>Review 容器作触发源：成员齐备后审批节点停等批准，批准放行后再触发目标启动。</summary>
    [Fact]
    public void Review_container_trigger_parks_until_approval()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewContainerTriggerFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot parked = harness.Settle(id);

        Assert.Equal(TaskState.Running, parked.State);
        Assert.Single(harness.Awaiting(id, AwaitKind.Approve));
        Assert.Empty(parked.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs);

        harness.Waits.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs);
    }

    /// <summary>来源先发布再触发下游，触发节点无数据输入。</summary>
    private const string TriggerOnceFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "来源", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "触发", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "来源@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>对话环：输入节点是挂点，回答每次驱动触发节点按新版本启动一轮，触发目标不接收回答内容。</summary>
    private const string TriggerLoopFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标", "回复": "回话@回复" } ] },
              { "Name": "回话", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "回答", "Type": "Signal" }], "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>数据与触发并行：触发源只发信号，目标只收数据源的产出。</summary>
    private const string MixedTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "数据", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "信号", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "信号", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "合并", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论" }, { "Name": "信号", "Type": "Signal" }], "From": [ { "结论": "数据@结论", "信号": "信号@信号" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>自动容器成员齐备放行后整体作触发源。</summary>
    private const string ContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "准备箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }], "Nodes": [ { "Name": "准备", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] } ] },
              { "Name": "触发", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "准备箱@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发引用可带命名端口：来源声明输出端口，端口放行即触发。</summary>
    private const string NamedPortTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "来源", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "信号", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "触发", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "信号", "Type": "Signal" }], "From": [ { "信号": "来源@信号" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>挂点触发环给出 MaxRuns 上限：三轮回答只启动两轮，第三问起不再有新 run。</summary>
    private const string TriggerLoopWithLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标", "回复": "回话@回复" } ] },
              { "Name": "回话", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "回答", "Type": "Signal" }], "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ], "MaxRuns": 2 }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义不写接线，引用处注入信号条目随实例生效。</summary>
    private const string LibraryTriggerInjectionFlow = """
        {
          "Nodes": [ { "Name": "信号工", "Kind": "Executable", "Inputs": [{ "Name": "结论", "Type": "Signal" }] } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "来源", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "触发实例", "Use": "信号工", "Model": "执行者", "From": [ { "结论": "来源@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>审批节点挡住信号驱动的触发节点，来源发布后停在待批准。</summary>
    private const string TriggerReviewFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "来源", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "来源@结论" } ] },
              { "Name": "触发", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "审阅@结论" } ] }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发节点声明输出校验，run 首轮不通过停在阻塞，返工重跑通过后放行。</summary>
    private const string TriggerValidateFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "来源", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
              { "Name": "触发", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "结论": "来源@结论" } ], "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>审查箱作触发源的载体：成员齐备经审批节点停等批准，批准放行后触发目标启动。</summary>
    private const string ReviewContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "审查箱", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "干工@结论" }], "Nodes": [ { "Name": "干工", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] } ] },
              { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "审查箱@结论" } ] },
              { "Name": "触发", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Signal" }], "From": [ { "结论": "审阅@结论" } ] }
            ] }
          ] } ]
        }
        """;
}
