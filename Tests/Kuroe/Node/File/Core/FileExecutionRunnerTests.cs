using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File.Tests;

/// <summary>文件执行器：按路径变量读写文件，成功交回内容，失败交回原因。</summary>
public sealed class FileExecutionRunnerTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly PortName ContentPort = new("正文");

    [Fact]
    public async Task Read_returns_the_file_content()
    {
        using TempRoot root = new();
        await System.IO.File.WriteAllTextAsync(root.Resolve("in.txt"), "文件内容", TestContext.Current.CancellationToken);
        ExecutableNode node = ReadNode(root, "in.txt");
        FileReadExecutionRunner runner = new(node, Parameters(root, "in.txt", FileReadNodeTypes.DefaultMaxChars));

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(node, ("任务路径", root.DirectoryPath)),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ExecutionOutputKind.Content, result.Value.Kind);
        Assert.Equal("文件内容", result.Value.Content);
    }

    [Fact]
    public async Task Read_uses_the_root_variable()
    {
        using TempRoot root = new();
        System.IO.Directory.CreateDirectory(root.Resolve("sub"));
        await System.IO.File.WriteAllTextAsync(root.Resolve("sub/in.txt"), "基准内容", TestContext.Current.CancellationToken);
        ExecutableNode node = ReadNode(root, "in.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters(root, "in.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.Resolve("sub"))),
                CancellationToken.None);

        Assert.Equal("基准内容", result.Value.Content);
    }

    [Fact]
    public async Task Missing_file_fails()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "missing.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters(root, "missing.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath)),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("读取 {root}/missing.txt 失败", result.FirstError.Description);
    }

    [Fact]
    public async Task Read_truncates_at_the_declared_limit()
    {
        using TempRoot root = new();
        await System.IO.File.WriteAllTextAsync(root.Resolve("in.txt"), "abcdef", TestContext.Current.CancellationToken);
        ExecutableNode node = ReadNode(root, "in.txt", maxChars: 3);

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters(root, "in.txt", 3))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath)),
                CancellationToken.None);

        Assert.Equal("abc…内容已截断", result.Value.Content);
    }

    [Fact]
    public async Task Read_without_a_limit_reads_the_whole_file()
    {
        using TempRoot root = new();
        string content = new('a', 9000);
        await System.IO.File.WriteAllTextAsync(root.Resolve("in.txt"), content, TestContext.Current.CancellationToken);
        ExecutableNode node = ReadNode(root, "in.txt", maxChars: 0);

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters(root, "in.txt", 0))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath)),
                CancellationToken.None);

        Assert.Equal(content, result.Value.Content);
    }

    [Fact]
    public async Task Read_path_outside_the_workspace_fails()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "../escape.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters(root, "../escape.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath)),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Path {root}/../escape.txt 不是工作区内的合法路径", result.FirstError.Description);
    }

    [Fact]
    public async Task Variable_root_outside_the_workspace_fails()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "in.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters(root, "in.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", Path.GetFullPath("..", root.DirectoryPath))),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("不是工作区内的合法路径", result.FirstError.Description);
    }

    [Fact]
    public async Task Canceled_execution_does_not_read()
    {
        using TempRoot root = new();
        ExecutableNode node = ReadNode(root, "missing.txt");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new FileReadExecutionRunner(
                node,
                Parameters(root, "missing.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath)),
                cancellation.Token));
    }

    [Fact]
    public async Task Write_creates_parent_directories_and_returns_the_written_content()
    {
        using TempRoot root = new();
        ExecutableNode node = WriteNode(root, "out/nested/result.txt");

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters(root, "out/nested/result.txt", FileWriteNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath), ("正文", "写入内容")),
                CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("写入内容", result.Value.Content);
        Assert.Equal("写入内容", await System.IO.File.ReadAllTextAsync(root.Resolve("out/nested/result.txt"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Write_uses_the_base_path_input()
    {
        using TempRoot root = new();
        System.IO.Directory.CreateDirectory(root.Resolve("sub"));
        ExecutableNode node = WriteNode(root, "out.txt");

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters(root, "out.txt", FileWriteNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.Resolve("sub")), ("正文", "写入内容")),
                CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("写入内容", await System.IO.File.ReadAllTextAsync(root.Resolve("sub/out.txt"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Over_limit_content_fails_without_writing()
    {
        using TempRoot root = new();
        ExecutableNode node = WriteNode(root, "out.txt", maxChars: 100);

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters(root, "out.txt", 100))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath), ("正文", new string('a', 101))),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("写入内容超过上限 100 字符", result.FirstError.Description);
        Assert.False(System.IO.File.Exists(root.Resolve("out.txt")));
    }

    [Fact]
    public async Task Write_without_a_limit_writes_long_content()
    {
        using TempRoot root = new();
        string content = new('a', 50000);
        ExecutableNode node = WriteNode(root, "out.txt");

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters(root, "out.txt", FileWriteNodeTypes.DefaultMaxChars))
            .ExecuteAsync(
                Input(node, ("任务路径", root.DirectoryPath), ("正文", content)),
                CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(content, await System.IO.File.ReadAllTextAsync(root.Resolve("out.txt"), TestContext.Current.CancellationToken));
    }

    private static ExecutionInput Input(
        ExecutableNode node,
        params (string Port, string Text)[] texts) => new()
        {
            Task = new TaskId(1),
            Node = node.Path,
            Type = node.Type,
            ExecutionCount = 1,
            Texts = texts.ToDictionary(item => new PortName(item.Port), item => item.Text),
        };

    private static ExecutableNode ReadNode(
        TempRoot root,
        string relative,
        int maxChars = FileReadNodeTypes.DefaultMaxChars) => new()
        {
            Path = TargetPath,
            Type = FileReadNodeTypes.Create(Parameters(root, relative, maxChars)),
            Spec = new ExecutableSpec(),
            Inputs =
        [
            new InputPort(new PortName("任务路径"), PortTypes.Text),
            new InputPort(new PortName("触发"), PortTypes.Signal),
        ],
            Outputs = [new Port(ContentPort, PortTypes.Text)],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
            Incoming =
        [
            new InputBinding(
                TargetPath,
                TargetPath,
                new PortName("结果"),
                new PortName("触发"),
                PortTypes.Signal,
                Trigger: true),
            new InputBinding(TargetPath, TargetPath, new PortName("结果"), new PortName("任务路径"), PortTypes.Text),
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
            Inputs =
            [
                new InputPort(ContentPort, PortTypes.Text),
                new InputPort(new PortName("任务路径"), PortTypes.Text),
            ],
            Outputs = outputs ?? [],
            MaxExecutions = ExecutableNode.DefaultMaxExecutions,
            Incoming =
            [
                new InputBinding(TargetPath, TargetPath, new PortName("结果"), ContentPort, PortTypes.Text),
                new InputBinding(
                    TargetPath,
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