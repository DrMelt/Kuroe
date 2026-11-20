using Kuroe.Flow.Graph.Shared.StaticGraph;
using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Model;

using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Tasks.Shared;
using Kuroe.TestSupport;
using Xunit;
using ExecutableNode = Kuroe.Flow.Graph.Shared.StaticGraph.ExecutableNode;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.File.Tests;

/// <summary>文件执行器：按路径变量读写文件，成功交回内容，失败交回原因。</summary>
public sealed class FileExecutionRunnerTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly PortName ContentPort = new("正文");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();
    private readonly Entry _entry = new();

    [Fact(DisplayName = "读取返回文件内容")]
    public async Task Read_returns_the_file_content()
    {
        _files.AddFile(Path.Combine(_root, "in.txt"), "文件内容");
        ExecutableNode node = ReadNode("in.txt");
        FileReadExecutionRunner runner = Reader(Parameters("in.txt", FileReadNodeTypes.DefaultMaxChars));
        Input(node, ("任务路径", _root));

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ExecutionOutputKind.Content, result.Value.Kind);
        Assert.Equal("文件内容", result.Value.Content);
    }

    [Fact(DisplayName = "读取使用根变量")]
    public async Task Read_uses_the_root_variable()
    {
        _files.AddFile(Path.Combine(_root, "sub/in.txt"), "基准内容");
        ExecutableNode node = ReadNode("in.txt");
        Input(node, ("任务路径", Path.Combine(_root, "sub")));

        ErrorOr<ExecutionOutput> result = await Reader(Parameters("in.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.Equal("基准内容", result.Value.Content);
    }

    [Fact(DisplayName = "缺失文件失败")]
    public async Task Missing_file_fails()
    {
        ExecutableNode node = ReadNode("missing.txt");

        Input(node, ("任务路径", _root));

        ErrorOr<ExecutionOutput> result = await Reader(Parameters("missing.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("读取 {root}/missing.txt 失败", result.FirstError.Description);
    }

    [Fact(DisplayName = "读取按声明上限截断")]
    public async Task Read_truncates_at_the_declared_limit()
    {
        _files.AddFile(Path.Combine(_root, "in.txt"), "abcdef");
        ExecutableNode node = ReadNode("in.txt", maxChars: 3);

        Input(node, ("任务路径", _root));

        ErrorOr<ExecutionOutput> result = await Reader(Parameters("in.txt", 3))
            .ExecuteAsync(CancellationToken.None);

        Assert.Equal("abc…内容已截断", result.Value.Content);
    }

    [Fact(DisplayName = "没有上限时读取整个文件")]
    public async Task Read_without_a_limit_reads_the_whole_file()
    {
        string content = new('a', 9000);
        _files.AddFile(Path.Combine(_root, "in.txt"), content);
        ExecutableNode node = ReadNode("in.txt", maxChars: 0);

        Input(node, ("任务路径", _root));

        ErrorOr<ExecutionOutput> result = await Reader(Parameters("in.txt", 0))
            .ExecuteAsync(CancellationToken.None);

        Assert.Equal(content, result.Value.Content);
    }

    [Fact(DisplayName = "读取工作区之外的路径失败")]
    public async Task Read_path_outside_the_workspace_fails()
    {
        ExecutableNode node = ReadNode("../escape.txt");

        Input(node, ("任务路径", _root));

        ErrorOr<ExecutionOutput> result = await Reader(Parameters("../escape.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Path {root}/../escape.txt 不是工作区内的合法路径", result.FirstError.Description);
    }

    [Fact(DisplayName = "文件执行变量根在工作区之外失败")]
    public async Task Variable_root_outside_the_workspace_fails()
    {
        ExecutableNode node = ReadNode("in.txt");

        Input(node, ("任务路径", Path.GetFullPath("..", _root)));

        ErrorOr<ExecutionOutput> result = await Reader(Parameters("in.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("不是工作区内的合法路径", result.FirstError.Description);
    }

    [Fact(DisplayName = "已取消执行不读取")]
    public async Task Canceled_execution_does_not_read()
    {
        ExecutableNode node = ReadNode("missing.txt");
        Input(node, ("任务路径", _root));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Reader(Parameters("missing.txt", FileReadNodeTypes.DefaultMaxChars))
            .ExecuteAsync(cancellation.Token));
    }

    [Fact(DisplayName = "写入创建父目录并返回写入内容")]
    public async Task Write_creates_parent_directories_and_returns_the_written_content()
    {
        ExecutableNode node = WriteNode("out/nested/result.txt");

        Input(node, ("任务路径", _root), ("正文", "写入内容"));

        ErrorOr<ExecutionOutput> result = await Writer(Parameters("out/nested/result.txt", FileWriteNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("写入内容", result.Value.Content);
        Assert.Equal("写入内容", _files.ReadText(Path.Combine(_root, "out/nested/result.txt")));
    }

    [Fact(DisplayName = "写入使用基准路径输入")]
    public async Task Write_uses_the_base_path_input()
    {
        ExecutableNode node = WriteNode("out.txt");

        Input(node, ("任务路径", Path.Combine(_root, "sub")), ("正文", "写入内容"));

        ErrorOr<ExecutionOutput> result = await Writer(Parameters("out.txt", FileWriteNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("写入内容", _files.ReadText(Path.Combine(_root, "sub/out.txt")));
    }

    [Fact(DisplayName = "超限内容失败且不写入")]
    public async Task Over_limit_content_fails_without_writing()
    {
        ExecutableNode node = WriteNode("out.txt", maxChars: 100);

        Input(node, ("任务路径", _root), ("正文", new string('a', 101)));

        ErrorOr<ExecutionOutput> result = await Writer(Parameters("out.txt", 100))
            .ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("写入内容超过上限 100 字符", result.FirstError.Description);
        Assert.False(_files.FileExists(Path.Combine(_root, "out.txt")));
    }

    [Fact(DisplayName = "没有上限时写入长内容")]
    public async Task Write_without_a_limit_writes_long_content()
    {
        string content = new('a', 50000);
        ExecutableNode node = WriteNode("out.txt");

        Input(node, ("任务路径", _root), ("正文", content));

        ErrorOr<ExecutionOutput> result = await Writer(Parameters("out.txt", FileWriteNodeTypes.DefaultMaxChars))
            .ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(content, _files.ReadText(Path.Combine(_root, "out.txt")));
    }

    private void Input(
        ExecutableNode node,
        params (string Port, string Text)[] texts)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId("1"),
            new ExecutionStepId(node.Path.Value),
            [
                .. node.Inputs.Select(port => new ExecutionInputPort(
                    new ExecutionPortName(port.Name.Value),
                    port.Type.Semantics == PortSemantics.Signal ? ExecutionPortKind.Signal : ExecutionPortKind.Text,
                    port.Role == InputRole.Control ? ExecutionPortRole.Control : ExecutionPortRole.Content,
                    port.Role == InputRole.Content,
                    port.Default)),
            ],
            [],
            texts.ToDictionary(item => new ExecutionPortName(item.Port), item => item.Text),
            []);
        _entry.Set(request);
    }

    private FileReadExecutionRunner Reader(FileExecutionParameters parameters) =>
        new(parameters, _files, _entry);

    private FileWriteExecutionRunner Writer(FileExecutionParameters parameters) =>
        new(parameters, _files, _entry);

    private ExecutableNode ReadNode(
        string relative,
        int maxChars = FileReadNodeTypes.DefaultMaxChars) => new()
        {
            Path = TargetPath,
            Type = FileReadNodeTypes.Create(Parameters(relative, maxChars)),
            Spec = new ExecutableSpec(),
            Inputs =
        [
            new InputPort(new PortName("任务路径"), PortTypes.Text),
            new InputPort(new PortName("触发"), PortTypes.Signal),
        ],
            Outputs = [new Port(ContentPort, PortTypes.Text)],
            MaxExecutions = ExecutableSpec.DefaultMaxExecutions,
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
            Type = FileWriteNodeTypes.Create(Parameters(relative, maxChars)),
            Spec = new ExecutableSpec(),
            Inputs =
            [
                new InputPort(ContentPort, PortTypes.Text),
                new InputPort(new PortName("任务路径"), PortTypes.Text),
            ],
            Outputs = outputs ?? [],
            MaxExecutions = ExecutableSpec.DefaultMaxExecutions,
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

    private sealed class Entry : IExecutionRequestEntry
    {
        private ExecutionRequest? _request;

        public ExecutionRequest Request =>
            _request ?? throw new InvalidOperationException("测试请求尚未写入。");

        public void Set(ExecutionRequest request) => _request = request;

        public ExecutionId Id => new(1);

        public ExecutionStatus ReadStatus() => throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
            throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
            throw new NotSupportedException();
    }



    private FileExecutionParameters Parameters(string relative, int maxChars) => new()
    {
        Path = $"{{root}}/{relative}",
        Variables = new Dictionary<string, string> { ["root"] = "任务路径" },
        RootDirectory = _root,
        MaxChars = maxChars,
    };
}
