using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File.Tests;

/// <summary>文件节点的运行期对象：按版本读写文件，成功产出，失败阻塞并可返工。</summary>
public sealed class FileRuntimeTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly NodePath SourcePath = NodePath.Of("整体", "来源");
    private static readonly NodePath VariableSourcePath = NodePath.Of("整体", "基准");
    private static readonly PortName ContentPort = new("正文");

    [Fact]
    public void Read_produces_the_file_content_and_completes()
    {
        using TempRoot root = new();
        System.IO.File.WriteAllText(root.Resolve("in.txt"), "文件内容");
        ExecutableNode node = ReadNode(root, "in.txt");
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal(1, runtime.Revision);
        Assert.Equal("文件内容", runtime.PortText(context, ContentPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
        Assert.Null(runtime.TakeFailure());
    }

    [Fact]
    public void Read_uses_the_root_variable()
    {
        using TempRoot root = new();
        System.IO.Directory.CreateDirectory(root.Resolve("sub"));
        System.IO.File.WriteAllText(root.Resolve("sub/in.txt"), "基准内容");
        ExecutableNode node = ReadNode(root, "in.txt");
        StubNodeContext context = Context(node, root, root.Resolve("sub"));
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal("基准内容", runtime.PortText(context, ContentPort));
    }

    [Fact]
    public void Consuming_the_group_clears_readiness()
    {
        using TempRoot root = new();
        System.IO.File.WriteAllText(root.Resolve("in.txt"), "文件内容");
        ExecutableNode node = ReadNode(root, "in.txt");
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);
        NodeInputPlan plan = context.Plan(TargetPath)!;

        Assert.True(plan.Ready(runtime, context));
        ((IInputProgress)runtime).Consume(plan.Groups[0], context);

        Assert.False(plan.Ready(runtime, context));
    }

    [Fact]
    public void Missing_file_blocks_the_node_and_reports_once()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "missing.txt");
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);

        string? failure = runtime.TakeFailure();
        Assert.NotNull(failure);
        Assert.Contains("读取 {root}/missing.txt 失败", failure);
        Assert.Null(runtime.TakeFailure());
    }

    [Fact]
    public void Retry_reads_the_file_again_after_a_failure()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "later.txt");
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);
        runtime.Activate(context, Inputs(context));

        System.IO.File.WriteAllText(root.Resolve("later.txt"), "恢复内容");
        runtime.Retry();
        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal("恢复内容", runtime.PortText(context, ContentPort));
        Assert.Equal(NodeState.Done, runtime.Snapshot().State);
    }

    [Fact]
    public void Read_truncates_at_the_declared_limit()
    {
        using TempRoot root = new();
        System.IO.File.WriteAllText(root.Resolve("in.txt"), "abcdef");
        ExecutableNode node = ReadNode(root, "in.txt", maxChars: 3);
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal("abc…内容已截断", runtime.PortText(context, ContentPort));
    }

    [Fact]
    public void Read_without_a_limit_reads_the_whole_file()
    {
        using TempRoot root = new();
        string content = new('a', 9000);
        System.IO.File.WriteAllText(root.Resolve("in.txt"), content);
        ExecutableNode node = ReadNode(root, "in.txt", maxChars: 0);
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(content, runtime.PortText(context, ContentPort));
    }

    [Fact]
    public void Canceled_node_does_not_read()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "missing.txt");
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Cancel();
        runtime.Activate(context, Inputs(context));

        Assert.False(runtime.Released);
        Assert.Equal(NodeState.Canceled, runtime.Snapshot().State);
    }

    [Fact]
    public void Read_path_outside_the_workspace_blocks_the_node()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "../escape.txt");
        StubNodeContext context = Context(node, root);
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Contains("Path {root}/../escape.txt 不是工作区内的合法路径", runtime.TakeFailure());
    }

    [Fact]
    public void Variable_root_outside_the_workspace_blocks_the_node()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "in.txt");
        StubNodeContext context = Context(node, root, Path.GetFullPath("..", root.DirectoryPath));
        FileReadRuntime runtime = ReadRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Contains("不是工作区内的合法路径", runtime.TakeFailure());
    }

    [Fact]
    public void Write_creates_parent_directories_and_returns_the_written_content()
    {
        using TempRoot root = new();
        ExecutableNode node = WriteNode(root, "out/nested/result.txt",
            outputs: [new Port(new PortName("回执"), PortTypes.Text)]);
        StubNodeContext context = Context(node, root);
        context.AddSource(new SourceRuntime(SourcePath, "写入内容"));
        FileWriteRuntime runtime = WriteRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal("写入内容", System.IO.File.ReadAllText(root.Resolve("out/nested/result.txt")));
        Assert.Equal("写入内容", runtime.PortText(context, new PortName("回执")));

        ContextValue value = Assert.Single(runtime.OutputValues(context, new PortName("回执")));
        Assert.IsType<NodeSource>(value.Origin);
    }

    [Fact]
    public void Write_uses_the_base_path_input()
    {
        using TempRoot root = new();
        System.IO.Directory.CreateDirectory(root.Resolve("sub"));
        ExecutableNode node = WriteNode(root, "out.txt");
        StubNodeContext context = Context(node, root, root.Resolve("sub"));
        context.AddSource(new SourceRuntime(SourcePath, "写入内容"));
        FileWriteRuntime runtime = WriteRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal("写入内容", System.IO.File.ReadAllText(root.Resolve("sub/out.txt")));
    }

    [Fact]
    public void Over_limit_content_blocks_the_node_without_writing()
    {
        using TempRoot root = new();
        ExecutableNode node = WriteNode(root, "out.txt", maxChars: 100);
        StubNodeContext context = Context(node, root);
        context.AddSource(new SourceRuntime(SourcePath, new string('a', 101)));
        FileWriteRuntime runtime = WriteRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.Equal(NodeState.Blocked, runtime.Snapshot().State);
        Assert.Contains("写入内容超过上限 100 字符", runtime.TakeFailure());
        Assert.False(System.IO.File.Exists(root.Resolve("out.txt")));
    }

    [Fact]
    public void Write_without_a_limit_writes_long_content()
    {
        using TempRoot root = new();
        string content = new('a', 50000);
        ExecutableNode node = WriteNode(root, "out.txt");
        StubNodeContext context = Context(node, root);
        context.AddSource(new SourceRuntime(SourcePath, content));
        FileWriteRuntime runtime = WriteRuntime(node, context);

        runtime.Activate(context, Inputs(context));

        Assert.True(runtime.Released);
        Assert.Equal(content, System.IO.File.ReadAllText(root.Resolve("out.txt")));
    }

    private static StubNodeContext Context(ExecutableNode node, TempRoot root, string? rootValue = null)
    {
        StubNodeContext context = new(node);
        context.AddSource(new SourceRuntime(VariableSourcePath, rootValue ?? root.DirectoryPath));

        return context;
    }

    private static NodeInputValues Inputs(StubNodeContext context) =>
        NodeInputValues.From(context.Plan(TargetPath)!.Groups[0], context);

    private static FileReadRuntime ReadRuntime(ExecutableNode node, StubNodeContext context) =>
        Assert.IsType<FileReadRuntime>(node.Type.Create(node, context));

    private static FileWriteRuntime WriteRuntime(ExecutableNode node, StubNodeContext context) =>
        Assert.IsType<FileWriteRuntime>(node.Type.Create(node, context));

    private static ExecutableNode ReadNode(
        TempRoot root,
        string relative,
        int maxChars = FileReadNodeTypes.DefaultMaxChars) => new()
        {
            Path = TargetPath,
            Type = FileReadNodeTypes.Create(Parameters(root, relative, maxChars)),
            Spec = new ExecutableSpec(),
            Model = null,
            Inputs =
        [
            new InputPort(new PortName("任务路径"), PortTypes.Text),
            new InputPort(new PortName("触发"), PortTypes.Signal),
        ],
            Outputs = [new Port(ContentPort, PortTypes.Text)],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
            Incoming =
        [
            new FlowEdge(SourcePath, TargetPath, new PortName("结果"), new PortName("触发"), PortTypes.Signal),
            new FlowEdge(VariableSourcePath, TargetPath, new PortName("结果"), new PortName("任务路径"), PortTypes.Text),
        ],
        };

    private static ExecutableNode WriteNode(
        TempRoot root,
        string relative,
        Port[]? outputs = null,
        int maxChars = FileWriteNodeTypes.DefaultMaxChars) => new()
        {
            Path = TargetPath,
            Type = FileWriteNodeTypes.Create(Parameters(root, relative, maxChars)),
            Spec = new ExecutableSpec(),
            Model = null,
            Inputs =
            [
                new InputPort(ContentPort, PortTypes.Text),
                new InputPort(new PortName("任务路径"), PortTypes.Text),
            ],
            Outputs = outputs ?? [],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
            Incoming =
            [
                new FlowEdge(SourcePath, TargetPath, new PortName("结果"), ContentPort, PortTypes.Text),
                new FlowEdge(
                    VariableSourcePath,
                    TargetPath,
                    new PortName("结果"),
                    new PortName("任务路径"),
                    PortTypes.Text),
            ],
        };

    private static FileParameters Parameters(TempRoot root, string relative, int maxChars) => new()
    {
        Path = $"{{root}}/{relative}",
        Variables = new Dictionary<string, string> { ["root"] = "任务路径" },
        RootDirectory = root.DirectoryPath,
        MaxChars = maxChars,
    };
}