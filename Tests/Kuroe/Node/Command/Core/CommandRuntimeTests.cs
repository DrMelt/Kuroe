using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Process;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令节点的运行期对象：按版本执行命令，成功产出，失败阻塞并可返工。</summary>
public sealed class CommandRuntimeTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly NodePath SourcePath = NodePath.Of("整体", "来源");
    private static readonly NodePath VariableSourcePath = NodePath.Of("整体", "基准");
    private static readonly NodePath RepositorySourcePath = NodePath.Of("整体", "仓库");
    private static readonly NodePath BranchSourcePath = NodePath.Of("整体", "分支");
    private static readonly NodePath SubdirectorySourcePath = NodePath.Of("整体", "子目录");
    private static readonly PortName OutputPort = new("输出");
    private static readonly string Root = Path.GetTempPath();

    [Fact]
    public void Execute_produces_the_output_and_completes()
    {
        FakeProcessRunner process = new FakeProcessRunner()
            .Returns($"甲{Environment.NewLine}乙{Environment.NewLine}");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal(1, runtime.Revision);
        Assert.Equal($"甲{Environment.NewLine}乙", runtime.PortText(OutputPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Null(runtime.TakeFailure());
    }

    [Fact]
    public void Consuming_the_group_clears_readiness()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("第一次");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);
        NodeInputPlan plan = Assert.IsType<NodeInputPlan>(context.Plan(TargetPath));

        Assert.True(plan.Ready(runtime, context));
        ((IInputProgress)runtime).Consume(plan.Groups[0], context);

        Assert.False(plan.Ready(runtime, context));
    }

    [Fact]
    public void Non_zero_exit_code_publishes_the_output_by_default()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal("错误", runtime.PortText(OutputPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Null(runtime.TakeFailure());
    }

    [Fact]
    public void Non_zero_exit_code_blocks_when_declared()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        ExecutableNode node = Node(process, failOnNonZeroExitCode: true);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);

        string? failure = runtime.TakeFailure();
        Assert.NotNull(failure);
        Assert.Contains("退出码 2", failure);
        Assert.Contains("错误", failure);
    }

    [Fact]
    public void Non_zero_exit_code_without_output_publishes_empty_content()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns(string.Empty, exitCode: 1);
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Null(runtime.PortText(OutputPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Null(runtime.TakeFailure());
    }

    [Fact]
    public void Timeout_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("部分输出", timedOut: true);
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Equal("命令在 5 秒内未结束，已终止。", runtime.TakeFailure());
    }

    [Fact]
    public void Start_failure_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Throws("无法启动 dotnet。");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Equal("无法启动 dotnet。", runtime.TakeFailure());
    }

    [Fact]
    public void Retry_runs_the_command_again_after_a_failure()
    {
        FakeProcessRunner process = new FakeProcessRunner()
            .Throws("无法启动 dotnet。")
            .Returns("恢复内容");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);
        runtime.Activate(context, Inputs(context));

        runtime.Retry();
        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal("恢复内容", runtime.PortText(OutputPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Equal(2, process.Calls);
    }

    [Fact]
    public void Execute_passes_program_arguments_directory_and_timeout()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal("dotnet", process.Program);
        Assert.Equal(["--version"], process.Arguments);
        Assert.Equal(Root, process.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(5), process.Timeout);
    }

    [Fact]
    public void Arguments_expand_variables_without_path_validation()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        InputPort[] inputs =
        [
            new(new PortName("任务路径"), PortTypes.Text),
            new(new PortName("仓库地址"), PortTypes.Text),
            new(new PortName("分支名"), PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
        ];
        InputBinding[] incoming =
        [
            new(TargetPath, SourcePath, new PortName("结果"), new PortName("触发"), PortTypes.Signal, Trigger: true),
            new(TargetPath, VariableSourcePath, new PortName("结果"), new PortName("任务路径"), PortTypes.Text),
            new(TargetPath, RepositorySourcePath, new PortName("结果"), new PortName("仓库地址"), PortTypes.Text),
            new(TargetPath, BranchSourcePath, new PortName("结果"), new PortName("分支名"), PortTypes.Text),
        ];
        ExecutableNode node = Node(
            process,
            arguments: ["clone", "--branch", "{分支名}", "{仓库地址}", "."],
            variables: new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["仓库地址"] = "仓库地址",
                ["分支名"] = "分支名",
            },
            inputs: inputs,
            incoming: incoming);
        StubNodeContext context = Sources(
            node,
            (VariableSourcePath, Root),
            (RepositorySourcePath, @"D:\outside\repo.git"),
            (BranchSourcePath, "feature/x"));
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(["clone", "--branch", "feature/x", @"D:\outside\repo.git", "."], process.Arguments);
        Assert.Equal(Root, process.WorkingDirectory);
    }

    [Fact]
    public void Unmapped_argument_placeholder_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process, arguments: ["clone", "{missing}"]);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Contains("含未映射的变量 missing", runtime.TakeFailure());
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public void Execute_uses_the_root_variable_as_the_default_directory()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node, Path.Combine(Root, "sub"));
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact]
    public void Execute_resolves_the_declared_directory_from_the_root_variable()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process, directory: "sub");
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact]
    public void Directory_outside_the_workspace_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process, directory: "../escape");
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Contains("Directory {root}/../escape 不是工作区内的合法路径", runtime.TakeFailure());
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public void Relative_root_is_resolved_before_expanding_the_directory()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process, directoryTemplate: "{root}");
        StubNodeContext context = Context(node, "sub");
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact]
    public void Relative_root_is_the_base_of_the_directory_template()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process, directoryTemplate: "{root}/src");
        StubNodeContext context = Context(node, "sub");
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(Path.Combine(Root, "sub", "src"), process.WorkingDirectory);
    }

    [Fact]
    public void Directory_variables_are_relative_to_the_root()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        InputPort[] inputs =
        [
            new(new PortName("任务路径"), PortTypes.Text),
            new(new PortName("子目录"), PortTypes.Text),
            new(new PortName("触发"), PortTypes.Signal),
        ];
        InputBinding[] incoming =
        [
            new(TargetPath, SourcePath, new PortName("结果"), new PortName("触发"), PortTypes.Signal, Trigger: true),
            new(TargetPath, VariableSourcePath, new PortName("结果"), new PortName("任务路径"), PortTypes.Text),
            new(TargetPath, SubdirectorySourcePath, new PortName("结果"), new PortName("子目录"), PortTypes.Text),
        ];
        ExecutableNode node = Node(
            process,
            directoryTemplate: "{root}/{sub}",
            variables: new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["sub"] = "子目录",
            },
            inputs: inputs,
            incoming: incoming);
        StubNodeContext context = Sources(node, (VariableSourcePath, "task"), (SubdirectorySourcePath, "src"));
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(Path.Combine(Root, "task", "src"), process.WorkingDirectory);
    }

    [Fact]
    public void Variable_root_outside_the_workspace_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process);
        StubNodeContext context = Context(node, Path.GetFullPath("..", Root));
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Contains("不是工作区内的合法路径", runtime.TakeFailure());
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public void Output_is_truncated_at_the_declared_limit()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("abcdef");
        ExecutableNode node = Node(process, maxChars: 3);
        StubNodeContext context = Context(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal("abc…输出已截断", runtime.PortText(OutputPort));
    }

    private static StubNodeContext Context(ExecutableNode node, string? rootValue = null)
    {
        StubNodeContext context = new(node);
        context.AddSource(new SourceRuntime(VariableSourcePath, rootValue ?? Root));

        return context;
    }

    private static StubNodeContext Sources(ExecutableNode node, params (NodePath Path, string Value)[] sources)
    {
        StubNodeContext context = new(node);
        foreach ((NodePath path, string value) in sources)
        {
            context.AddSource(new SourceRuntime(path, value));
        }

        return context;
    }

    private static NodeInputValues Inputs(StubNodeContext context) =>
        NodeInputValues.From(Assert.IsType<NodeInputPlan>(context.Plan(TargetPath)).Groups[0], context);

    private static CommandRuntime Runtime(ExecutableNode node, StubNodeContext context) =>
        Assert.IsType<CommandRuntime>(((INodeRuntimeFactory)node.Type).Create(node, context));

    private static ExecutableNode Node(
        FakeProcessRunner process,
        int maxChars = CommandNodeTypes.DefaultMaxChars,
        string? directory = null,
        bool failOnNonZeroExitCode = false,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? variables = null,
        InputPort[]? inputs = null,
        InputBinding[]? incoming = null,
        string? directoryTemplate = null) => new()
        {
            Path = TargetPath,
            Type = CommandNodeTypes.Create(
                Parameters(maxChars, directory, failOnNonZeroExitCode, arguments, variables, directoryTemplate),
                process),
            Spec = new ExecutableSpec(),
            Inputs = inputs ??
            [
                new InputPort(new PortName("任务路径"), PortTypes.Text),
                new InputPort(new PortName("触发"), PortTypes.Signal),
            ],
            Outputs = [new Port(OutputPort, PortTypes.Text)],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
            Incoming = incoming ??
            [
                new InputBinding(
                    TargetPath,
                    SourcePath,
                    new PortName("结果"),
                    new PortName("触发"),
                    PortTypes.Signal,
                    Trigger: true),
                new InputBinding(
                    TargetPath,
                    VariableSourcePath,
                    new PortName("结果"),
                    new PortName("任务路径"),
                    PortTypes.Text),
            ],
        };

    private static CommandParameters Parameters(
        int maxChars,
        string? directory,
        bool failOnNonZeroExitCode,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? variables = null,
        string? directoryTemplate = null) => new()
        {
            Program = "dotnet",
            Arguments = arguments ?? ["--version"],
            Directory = directoryTemplate ?? (directory is null ? null : $"{{root}}/{directory}"),
            Variables = variables ?? new Dictionary<string, string> { ["root"] = "任务路径" },
            RootDirectory = Root,
            TimeoutSeconds = 5,
            MaxChars = maxChars,
            FailOnNonZeroExitCode = failOnNonZeroExitCode,
        };

    /// <summary>按入队顺序给出进程结果的替身。</summary>
    private sealed class FakeProcessRunner : IProcessRunner
    {
        private readonly Queue<Func<ProcessResult>> _results = new();

        public int Calls { get; private set; }

        public string? Program { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public string? WorkingDirectory { get; private set; }

        public TimeSpan Timeout { get; private set; }

        public FakeProcessRunner Returns(string output, int exitCode = 0, bool timedOut = false)
        {
            _results.Enqueue(() => new ProcessResult(output, exitCode, timedOut));

            return this;
        }

        public FakeProcessRunner Throws(string message)
        {
            _results.Enqueue(() => throw new ProcessStartException(message));

            return this;
        }

        public ProcessResult Run(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Calls++;
            Program = program;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            Timeout = timeout;

            return _results.Count > 0
                ? _results.Dequeue()()
                : throw new InvalidOperationException("测试未给出进程结果。");
        }
    }
}