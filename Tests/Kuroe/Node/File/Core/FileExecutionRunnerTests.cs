using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.TestSupport;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File.Tests;

/// <summary>文件执行器：按路径变量读写文件，成功交回内容，失败交回原因。</summary>
public sealed class FileExecutionRunnerTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly PortName ContentPort = new("正文");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact]
    public async Task Read_returns_the_file_content()
    {
        _files.AddFile(Path.Combine(_root, "in.txt"), "文件内容");
        ExecutableNode node = ReadNode("in.txt");
        FileReadExecutionRunner runner = new(node, Parameters("in.txt", FileReadNodeTypes.DefaultMaxChars), _files);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(node, ("任务路径", _root)),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ExecutionOutputKind.Content, result.Value.Kind);
        Assert.Equal("文件内容", result.Value.Content);
    }

    [Fact]
    public async Task Read_uses_the_root_variable()
    {
        _files.AddFile(Path.Combine(_root, "sub/in.txt"), "基准内容");
        ExecutableNode node = ReadNode("in.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters("in.txt", FileReadNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", Path.Combine(_root, "sub"))),
                CancellationToken.None);

        Assert.Equal("基准内容", result.Value.Content);
    }

    [Fact]
    public async Task Missing_file_fails()
    {
        ExecutableNode node = ReadNode("missing.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters("missing.txt", FileReadNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root)),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("读取 {root}/missing.txt 失败", result.FirstError.Description);
    }

    [Fact]
    public async Task Read_truncates_at_the_declared_limit()
    {
        _files.AddFile(Path.Combine(_root, "in.txt"), "abcdef");
        ExecutableNode node = ReadNode("in.txt", maxChars: 3);

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters("in.txt", 3),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root)),
                CancellationToken.None);

        Assert.Equal("abc…内容已截断", result.Value.Content);
    }

    [Fact]
    public async Task Read_without_a_limit_reads_the_whole_file()
    {
        string content = new('a', 9000);
        _files.AddFile(Path.Combine(_root, "in.txt"), content);
        ExecutableNode node = ReadNode("in.txt", maxChars: 0);

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters("in.txt", 0),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root)),
                CancellationToken.None);

        Assert.Equal(content, result.Value.Content);
    }

    [Fact]
    public async Task Read_path_outside_the_workspace_fails()
    {
        ExecutableNode node = ReadNode("../escape.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters("../escape.txt", FileReadNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root)),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Path {root}/../escape.txt 不是工作区内的合法路径", result.FirstError.Description);
    }

    [Fact]
    public async Task Variable_root_outside_the_workspace_fails()
    {
        ExecutableNode node = ReadNode("in.txt");

        ErrorOr<ExecutionOutput> result = await new FileReadExecutionRunner(
                node,
                Parameters("in.txt", FileReadNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", Path.GetFullPath("..", _root))),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("不是工作区内的合法路径", result.FirstError.Description);
    }

    [Fact]
    public async Task Canceled_execution_does_not_read()
    {
        ExecutableNode node = ReadNode("missing.txt");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new FileReadExecutionRunner(
                node,
                Parameters("missing.txt", FileReadNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root)),
                cancellation.Token));
    }

    [Fact]
    public async Task Write_creates_parent_directories_and_returns_the_written_content()
    {
        ExecutableNode node = WriteNode("out/nested/result.txt");

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters("out/nested/result.txt", FileWriteNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root), ("正文", "写入内容")),
                CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("写入内容", result.Value.Content);
        Assert.Equal("写入内容", _files.ReadText(Path.Combine(_root, "out/nested/result.txt")));
    }

    [Fact]
    public async Task Write_uses_the_base_path_input()
    {
        ExecutableNode node = WriteNode("out.txt");

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters("out.txt", FileWriteNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", Path.Combine(_root, "sub")), ("正文", "写入内容")),
                CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("写入内容", _files.ReadText(Path.Combine(_root, "sub/out.txt")));
    }

    [Fact]
    public async Task Over_limit_content_fails_without_writing()
    {
        ExecutableNode node = WriteNode("out.txt", maxChars: 100);

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters("out.txt", 100),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root), ("正文", new string('a', 101))),
                CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("写入内容超过上限 100 字符", result.FirstError.Description);
        Assert.False(_files.FileExists(Path.Combine(_root, "out.txt")));
    }

    [Fact]
    public async Task Write_without_a_limit_writes_long_content()
    {
        string content = new('a', 50000);
        ExecutableNode node = WriteNode("out.txt");

        ErrorOr<ExecutionOutput> result = await new FileWriteExecutionRunner(
                node,
                Parameters("out.txt", FileWriteNodeTypes.DefaultMaxChars),
                _files)
            .ExecuteAsync(
                Input(node, ("任务路径", _root), ("正文", content)),
                CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(content, _files.ReadText(Path.Combine(_root, "out.txt")));
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

    private ExecutableNode ReadNode(
        string relative,
        int maxChars = FileReadNodeTypes.DefaultMaxChars) => new()
        {
            Path = TargetPath,
            Type = FileReadNodeTypes.Create(Parameters(relative, maxChars), _files),
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
                BindingSource.ForNode(TargetPath, new PortName("结果")),
                new PortName("触发"),
                PortTypes.Signal,
                Trigger: true),
            new InputBinding(
                TargetPath,
                BindingSource.ForNode(TargetPath, new PortName("结果")),
                new PortName("任务路径"),
                PortTypes.Text),
        ],
        };

    private ExecutableNode WriteNode(
        string relative,
        Port[]? outputs = null,
        int maxChars = FileWriteNodeTypes.DefaultMaxChars) => new()
        {
            Path = TargetPath,
            Type = FileWriteNodeTypes.Create(Parameters(relative, maxChars), _files),
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
                new InputBinding(
                TargetPath,
                BindingSource.ForNode(TargetPath, new PortName("结果")),
                ContentPort,
                PortTypes.Text),
                new InputBinding(
                    TargetPath,
                    BindingSource.ForNode(TargetPath, new PortName("结果")),
                    new PortName("任务路径"),
                    PortTypes.Text),
            ],
        };

    private FileParameters Parameters(string relative, int maxChars) => new()
    {
        Path = $"{{root}}/{relative}",
        Variables = new Dictionary<string, string> { ["root"] = "任务路径" },
        RootDirectory = _root,
        MaxChars = maxChars,
    };
}