using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>上游接线条目：无标记条目进上下文供模型取用，Signal 标记只作触发信号不进上下文，
/// Or 标记把条目归入可选启动组，组内产出一并取用、任一组全齐备即启动。来源每次发布未消费的新版本触发一次启动。</summary>
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
        Assert.Contains("目标：目标", triggered.Context.Instruction);
        Assert.DoesNotContain(triggered.Context.Seed, message => message.Text.Contains("节点「来源」的产出"));
    }

    /// <summary>来源每次回答发布新版本触发一次：两轮回答两次触发，触发目标始终收不到内容。</summary>
    [Fact]
    public void Every_trigger_version_starts_one_run_without_data()
    {
        using KuroeHarness harness = KuroeHarness.Create(TriggerLoopFlow);

        TaskId id = harness.Submit("对话");
        harness.Wait(id, snapshot => snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));

        harness.Tasks.Answer(id, null, "第一问").ThrowIfError();
        harness.Wait(id, snapshot => snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));
        RunSnapshot first = harness.Executor.Started
            .Single(run => run.Context.NodeName.Value == "回话" && run.Context.ExecutionCount == 1)
            .Snapshot();
        Assert.DoesNotContain(first.Context.Seed, message => message.Text.Contains("第一问"));

        harness.Tasks.Answer(id, null, "第二问").ThrowIfError();
        TaskSnapshot done = harness.Wait(id, snapshot => snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));
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
        harness.Wait(id, snapshot => snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));

        harness.Tasks.Answer(id, null, "第一问").ThrowIfError();
        harness.Wait(id, snapshot => harness.Executor.Started.Count == 1 && snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));
        harness.Tasks.Answer(id, null, "第二问").ThrowIfError();
        harness.Wait(id, snapshot => harness.Executor.Started.Count == 2 && snapshot.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput));
        harness.Tasks.Answer(id, null, "第三问").ThrowIfError();

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

    /// <summary>触发节点可叠加 Review 门控：触发启动后产出停在待批准，批准后放行。</summary>
    [Fact]
    public void Trigger_with_review_gate_parks_until_approval()
    {
        using KuroeHarness harness = KuroeHarness.Create(TriggerReviewFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot parked = harness.Settle(id);

        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Equal(NodeState.AwaitingApproval, Assert.Single(parked.ExecutableStates, state => state.Index == 2).State);

        harness.Tasks.Approve(id).ThrowIfError();
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
            if (run.Context.NodeIndex == 1)
            {
                return "来源产出";
            }

            return run.Context.ExecutionCount == 1 ? "初稿不够好" : "修订通过";
        };
        harness.Executor.PortValuesJsonFor = run => run.Context.NodeIndex switch
        {
            1 => """{"结论": "来源产出"}""",
            2 => run.Context.ExecutionCount == 1
                ? """{"结论": "初稿不够好"}"""
                : """{"结论": "修订通过"}""",
            _ => null,
        };

        TaskId id = harness.Submit("目标");
        TaskSnapshot blocked = harness.Settle(id);

        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(NodeState.Blocked, Assert.Single(blocked.ExecutableStates, state => state.Index == 2).State);

        harness.Tasks.Rework(id, null).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs.Count);
    }
    /// <summary>Review 容器作触发源：成员齐备后容器停等批准，批准放行后再触发目标启动。</summary>
    [Fact]
    public void Review_container_trigger_parks_until_approval()
    {
        using KuroeHarness harness = KuroeHarness.Create(ReviewContainerTriggerFlow);

        TaskId id = harness.Submit("目标");
        TaskSnapshot parked = harness.Settle(id);

        Assert.Equal(TaskState.AwaitingApproval, parked.State);
        Assert.Empty(parked.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs);

        harness.Tasks.Approve(id).ThrowIfError();
        TaskSnapshot done = harness.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Single(done.Executables.Single(entry => entry.Executable.Name.Value == "触发").Runs);
    }

    /// <summary>来源先发布再触发下游，触发节点无数据输入。</summary>
    private const string TriggerOnceFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "来源", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "触发", "Output": "Text", "Model": "执行者", "From": [{ "Node": "来源@结论", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    /// <summary>对话环：输入节点是挂点，回答每次驱动触发节点按新版本启动一轮，触发目标不接收回答内容。</summary>
    private const string TriggerLoopFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "收话", "Output": "Input", "Outputs": ["回答"], "From": ["回话@回复"] },
              { "Name": "回话", "Output": "Text", "Model": "执行者", "Outputs": ["回复"], "From": [{ "Node": "收话@回答", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    /// <summary>数据与触发并行：触发源只发信号，目标只收数据源的产出。</summary>
    private const string MixedTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "数据", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "信号", "Output": "Text", "Model": "执行者", "Outputs": ["信号"] },
              { "Name": "合并", "Output": "Text", "Model": "执行者", "From": ["数据@结论", { "Node": "信号@信号", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    /// <summary>自动容器成员齐备放行后整体作触发源。</summary>
    private const string ContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "准备箱", "Out": { "结论": "准备@结论" }, "Nodes": [ { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] } ] },
              { "Name": "触发", "Output": "Text", "Model": "执行者", "From": [{ "Node": "准备箱@结论", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发引用可带命名端口：来源声明输出端口，端口放行即触发。</summary>
    private const string NamedPortTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "来源", "Output": "Text", "Model": "执行者", "Outputs": ["信号"] },
              { "Name": "触发", "Output": "Text", "Model": "执行者", "From": [{ "Node": "来源@信号", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    /// <summary>挂点触发环给出 MaxRuns 上限：三轮回答只启动两轮，第三问起不再有新 run。</summary>
    private const string TriggerLoopWithLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "收话", "Output": "Input", "Outputs": ["回答"], "From": ["回话@回复"] },
              { "Name": "回话", "Output": "Text", "Model": "执行者", "Outputs": ["回复"], "From": [{ "Node": "收话@回答", "Signal": true }], "MaxRuns": 2 }
            ] }
          ] } ]
        }
        """;

    /// <summary>库执行定义不写接线，引用处注入信号条目随实例生效。</summary>
    private const string LibraryTriggerInjectionFlow = """
        {
          "Nodes": [ { "Name": "信号工", "Output": "Text" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "来源", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "触发实例", "Use": "信号工", "Model": "执行者", "From": [{ "Node": "来源@结论", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发节点叠加 Review 门控，产出停在待批准。</summary>
    private const string TriggerReviewFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "来源", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "触发", "Output": "Text", "Model": "执行者", "From": [{ "Node": "来源@结论", "Signal": true }], "Gate": "Review" }
            ] }
          ] } ]
        }
        """;

    /// <summary>触发节点声明输出校验，run 首轮不通过停在阻塞，返工重跑通过后放行。</summary>
    private const string TriggerValidateFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "来源", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
              { "Name": "触发", "Output": "Text", "Model": "执行者", "Outputs": ["结论"], "From": [{ "Node": "来源@结论", "Signal": true }], "Validate": { "Predicate": "TextContains", "Argument": "通过" } }
            ] }
          ] } ]
        }
        """;

    /// <summary>Review 容器作触发源：成员齐备停等批准，批准放行后触发目标启动。</summary>
    private const string ReviewContainerTriggerFlow = """
        {
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "审查箱", "Gate": "Review", "Out": { "结论": "干工@结论" }, "Nodes": [ { "Name": "干工", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] } ] },
              { "Name": "触发", "Output": "Text", "Model": "执行者", "From": [{ "Node": "审查箱@结论", "Signal": true }] }
            ] }
          ] } ]
        }
        """;
}
