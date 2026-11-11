using ApiHub.Shared.Models;
using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>执行详情的渲染：直接构造执行快照，断言契约端口区。</summary>
public sealed class ExecutionDetailViewTests
{
    [Fact]
    public void Llm_execution_prints_the_required_ports()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        (TaskSnapshot task, ExecutionSnapshot execution) = Snapshot(cli);

        new ExecutionDetailView(terminal).Print(task, execution with
        {
            Input = execution.Input with
            {
                OutputPorts =
                [
                    new Port(new PortName("结论"), PortTypes.Text, "是否通过", mode: PortMode.Trigger),
                    new Port(new PortName("信号"), PortTypes.Signal, mode: PortMode.Trigger),
                ],
            },
        });

        Assert.Contains("契约端口 2 个", output.Output);
        Assert.Contains("结论·触发：是否通过、信号·Signal·触发", output.Output);
    }

    [Fact]
    public void Llm_execution_without_required_ports_reports_none()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        (TaskSnapshot task, ExecutionSnapshot execution) = Snapshot(cli);

        new ExecutionDetailView(terminal).Print(task, execution with
        {
            Input = execution.Input with { OutputPorts = [] },
        });

        Assert.Contains("契约端口 0 个", output.Output);
        Assert.Contains("本次执行没有需要交回的端口。", output.Output);
    }

    [Fact]
    public void Execution_without_llm_declaration_omits_the_section()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        (TaskSnapshot task, ExecutionSnapshot execution) = Snapshot(cli);

        new ExecutionDetailView(terminal).Print(task, execution with
        {
            Input = execution.Input with { Declaration = null },
        });

        Assert.DoesNotContain("契约端口", output.Output);
    }

    [Fact]
    public void Execution_with_a_non_llm_declaration_omits_the_section()
    {
        using ConsoleSession cli = new();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        (TaskSnapshot task, ExecutionSnapshot execution) = Snapshot(cli);

        new ExecutionDetailView(terminal).Print(task, execution with
        {
            Input = execution.Input with { Declaration = new OtherDeclaration() },
        });

        Assert.DoesNotContain("契约端口", output.Output);
    }

    [Fact]
    public void Input_values_show_source_port_and_truncation_hint()
    {
        using ConsoleSession cli = new();
        Terminal terminal = NewAnsiTerminal(out TestConsole output);
        (TaskSnapshot task, ExecutionSnapshot execution) = Snapshot(cli);
        NodePath node = execution.Input.Node;
        string longText = new('字', 71);
        ExecutionInput input = execution.Input with
        {
            Inputs =
            [
                new InputValue(
                    new PortName("实施要求"),
                    new ContextValue(new ConstantSource(NodePath.Of("上游"), new PortName("实施要求")), longText)),
                new InputValue(
                    new PortName("检查报告"),
                    new ContextValue(new DefaultSource(node, new PortName("检查报告")), "无")),
            ],
        };

        new ExecutionDetailView(terminal).Print(task, execution with { Input = input });

        Assert.Contains("来源", output.Output);
        Assert.Contains("端口", output.Output);
        Assert.Contains("节点「上游」常量端口「实施要求」", output.Output);
        Assert.Contains($"节点「{node}」默认值", output.Output);
        Assert.DoesNotContain("节点「上游」常量端口「实施要求」 · 实施要求", output.Output);
        Assert.Equal(70, output.Output.Count(character => character == '字'));
        Assert.Contains("\u001b[38;5;8m（已截断）", output.Output);
        Assert.DoesNotContain("…", output.Output);
    }

    /// <summary>建一套写 ANSI 序列的测试终端，用于断言提示颜色。</summary>
    private static Terminal NewAnsiTerminal(out TestConsole output)
    {
        TestConsole stdout = new();
        stdout.EmitAnsiSequences();
        TestConsole stderr = new();
        stderr.EmitAnsiSequences();
        output = stdout;

        return new Terminal(stdout, stderr);
    }

    /// <summary>不是 Llm 的宿主声明，用于固定契约端口区只对 Llm 执行显示。</summary>
    private sealed record OtherDeclaration : INodeDeclaration;

    private static (TaskSnapshot Task, ExecutionSnapshot Execution) Snapshot(ConsoleSession cli)
    {
        CompiledFlow compiled = cli.Environment.Flows.FindCompiled(new FlowName("默认")).ThrowIfError();
        IReadOnlyList<ExecutableSnapshot> executables =
        [
            .. compiled.Graph.ExecutableNodes.Select(node =>
                new ExecutableSnapshot(node, PortTexts.Empty, PortTexts.Empty, [], 0, node.MaxExecutions)),
        ];
        IReadOnlyList<ExecutableStateSnapshot> states =
        [
            .. compiled.Graph.ExecutableNodes.Select(node => new ExecutableStateSnapshot(node.Path, NodeState.Pending)),
        ];
        IReadOnlyList<ContainerSnapshot> containers =
        [
            .. compiled.Graph.Containers.Select(container => new ContainerSnapshot(
                container.Path,
                NodeState.Pending,
                [.. container.Children.Select(child => child.Path)],
                PortTexts.Empty,
                0,
                container.MaxExecutions)),
        ];

        TaskSnapshot task = new(
            new TaskId(1),
            "目标",
            new StartInputs([]),
            PortTexts.Empty,
            compiled.Model,
            compiled.Graph,
            TaskState.Running,
            0,
            executables,
            states,
            containers,
            [],
            0,
            At,
            At);

        ExecutableNode node = compiled.Graph.ExecutableNodes.Single(entry => entry.Label.Value == "制定计划");
        ExecutionInput input = new()
        {
            Task = task.Id,
            Node = node.Path,
            Type = node.Type,
            Declaration = new KuroeNodeDeclaration(ModelName.Create("fake").ThrowIfError()),
            OutputPorts = node.Outputs,
        };
        ExecutionSnapshot execution = new(
            new ExecutionId(1),
            input,
            ExecutionState.Succeeded,
            string.Empty,
            At,
            At,
            At,
            [],
            [],
            ExecutionUsage.None);

        return (task, execution);
    }

    /// <summary>固定的快照时间。</summary>
    private static readonly DateTimeOffset At = new(2026, 1, 2, 15, 4, 5, TimeSpan.FromHours(8));
}