using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Process;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令节点的运行期对象：按版本执行命令，成功产出，失败阻塞并可返工。</summary>
public sealed class CommandRuntimeTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly NodePath SourcePath = NodePath.Of("整体", "来源");
    private static readonly PortName OutputPort = new("输出");

    [Fact]
    public void Execute_produces_the_output_and_completes()
    {
        FakeProcessRunner process = new FakeProcessRunner()
            .Returns($"甲{Environment.NewLine}乙{Environment.NewLine}");
        ExecutableNode node = Node(process);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context);

        Assert.True(runtime.Released);
        Assert.Equal(1, runtime.Revision);
        Assert.Equal($"甲{Environment.NewLine}乙", runtime.PortText(context, OutputPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Null(runtime.TakeFailure());
    }

    [Fact]
    public void Without_a_new_source_version_it_does_not_run_again()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("第一次");
        ExecutableNode node = Node(process);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);
        runtime.Activate(context);

        runtime.Activate(context);

        Assert.Equal(1, process.Calls);
        Assert.Equal("第一次", runtime.PortText(context, OutputPort));
    }

    [Fact]
    public void Non_zero_exit_code_blocks_the_node_with_the_output()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        ExecutableNode node = Node(process);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context);

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);

        string? failure = runtime.TakeFailure();
        Assert.NotNull(failure);
        Assert.Contains("退出码 2", failure);
        Assert.Contains("错误", failure);
    }

    [Fact]
    public void Timeout_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("部分输出", timedOut: true);
        ExecutableNode node = Node(process);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context);

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Equal("命令在 5 秒内未结束，已终止。", runtime.TakeFailure());
    }

    [Fact]
    public void Start_failure_blocks_the_node()
    {
        FakeProcessRunner process = new FakeProcessRunner().Throws("无法启动 dotnet。");
        ExecutableNode node = Node(process);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context);

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
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);
        runtime.Activate(context);

        runtime.Retry();
        runtime.Activate(context);

        Assert.True(runtime.Released);
        Assert.Equal("恢复内容", runtime.PortText(context, OutputPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Equal(2, process.Calls);
    }

    [Fact]
    public void Execute_passes_program_arguments_directory_and_timeout()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        ExecutableNode node = Node(process);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context);

        Assert.Equal("dotnet", process.Program);
        Assert.Equal(["--version"], process.Arguments);
        Assert.Equal(Path.GetTempPath(), process.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(5), process.Timeout);
    }

    [Fact]
    public void Output_is_truncated_at_the_declared_limit()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("abcdef");
        ExecutableNode node = Node(process, maxChars: 3);
        StubNodeContext context = new(node);
        CommandRuntime runtime = Runtime(node, context);

        runtime.Activate(context);

        Assert.Equal("abc…输出已截断", runtime.PortText(context, OutputPort));
    }

    private static CommandRuntime Runtime(ExecutableNode node, StubNodeContext context) =>
        Assert.IsType<CommandRuntime>(node.Type.Create(node, context));

    private static ExecutableNode Node(FakeProcessRunner process, int maxChars = CommandNodeTypes.DefaultMaxChars) => new()
    {
        Path = TargetPath,
        Type = CommandNodeTypes.Create(Parameters(maxChars), process),
        Spec = new ExecutableSpec(),
        Model = null,
        Inputs = [new InputPort(new PortName("触发"), PortTypes.Signal)],
        Outputs = [new Port(OutputPort, PortTypes.Text)],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
        Incoming = [new FlowEdge(SourcePath, TargetPath, new PortName("结果"), new PortName("触发"), PortTypes.Signal)],
    };

    private static CommandParameters Parameters(int maxChars) => new()
    {
        Program = "dotnet",
        Arguments = ["--version"],
        FullDirectory = Path.GetTempPath(),
        TimeoutSeconds = 5,
        MaxChars = maxChars,
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