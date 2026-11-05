using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Process;
using Xunit;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令执行器：按命令变量执行进程，成功交回合并输出，失败交回原因。</summary>
public sealed class CommandExecutionRunnerTests
{
    private static readonly NodePath TargetPath = NodePath.Of("整体", "节点");
    private static readonly string Root = Path.GetTempPath();

    [Fact]
    public async Task Execute_returns_the_trimmed_output()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns($"甲{Environment.NewLine}乙{Environment.NewLine}");
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ExecutionOutputKind.Content, result.Value.Kind);
        Assert.Equal($"甲{Environment.NewLine}乙", result.Value.Content);
    }

    [Fact]
    public async Task Non_zero_exit_code_succeeds_by_default()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("错误", result.Value.Content);
    }

    [Fact]
    public async Task Non_zero_exit_code_fails_when_declared()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        CommandParameters parameters = Parameters(failOnNonZeroExitCode: true);
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("退出码 2", result.FirstError.Description);
        Assert.Contains("错误", result.FirstError.Description);
    }

    [Fact]
    public async Task Empty_output_succeeds_with_empty_content()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns(string.Empty, exitCode: 1);
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(string.Empty, result.Value.Content);
    }

    [Fact]
    public async Task Timeout_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("部分输出", timedOut: true);
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("命令在 5 秒内未结束，已终止。", result.FirstError.Description);
    }

    [Fact]
    public async Task Start_failure_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Throws("无法启动 dotnet。");
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("无法启动 dotnet。", result.FirstError.Description);
    }

    [Fact]
    public async Task Execute_passes_program_arguments_directory_and_timeout()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(Input(parameters, process), CancellationToken.None);

        Assert.Equal("dotnet", process.Program);
        Assert.Equal(["--version"], process.Arguments);
        Assert.Equal(Root, process.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(5), process.Timeout);
    }

    [Fact]
    public async Task Arguments_expand_variables_without_path_validation()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(
            arguments: ["clone", "--branch", "{分支名}", "{仓库地址}", "."],
            variables: new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["仓库地址"] = "仓库地址",
                ["分支名"] = "分支名",
            });
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(
            Input(parameters, process,
                ("任务路径", Root),
                ("仓库地址", @"D:\outside\repo.git"),
                ("分支名", "feature/x")),
            CancellationToken.None);

        Assert.Equal(["clone", "--branch", "feature/x", @"D:\outside\repo.git", "."], process.Arguments);
        Assert.Equal(Root, process.WorkingDirectory);
    }

    [Fact]
    public async Task Unmapped_argument_placeholder_fails_without_starting()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(arguments: ["clone", "{missing}"]);
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("含未映射的变量 missing", result.FirstError.Description);
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public async Task Execute_uses_the_root_variable_as_the_default_directory()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", Path.Combine(Root, "sub"))),
            CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact]
    public async Task Execute_resolves_the_declared_directory_from_the_root_variable()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(directory: "sub");
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", Root)),
            CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact]
    public async Task Directory_outside_the_workspace_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(directory: "../escape");
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", Root)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Directory {root}/../escape 不是工作区内的合法路径", result.FirstError.Description);
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public async Task Relative_root_is_resolved_before_expanding_the_directory()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(directoryTemplate: "{root}");
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", "sub")),
            CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact]
    public async Task Relative_root_is_the_base_of_the_directory_template()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(directoryTemplate: "{root}/src");
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", "sub")),
            CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub", "src"), process.WorkingDirectory);
    }

    [Fact]
    public async Task Directory_variables_are_relative_to_the_root()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters(
            directoryTemplate: "{root}/{sub}",
            variables: new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["sub"] = "子目录",
            });
        CommandExecutionRunner runner = new(parameters, process);

        await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", "task"), ("子目录", "src")),
            CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "task", "src"), process.WorkingDirectory);
    }

    [Fact]
    public async Task Variable_root_outside_the_workspace_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandParameters parameters = Parameters();
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process, ("任务路径", Path.GetFullPath("..", Root))),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("不是工作区内的合法路径", result.FirstError.Description);
        Assert.Equal(0, process.Calls);
    }

    [Fact]
    public async Task Output_is_truncated_at_the_declared_limit()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("abcdef");
        CommandParameters parameters = Parameters(maxChars: 3);
        CommandExecutionRunner runner = new(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(
            Input(parameters, process),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("abc…输出已截断", result.Value.Content);
    }

    private static ExecutionInput Input(
        CommandParameters parameters,
        FakeProcessRunner process,
        params (string Port, string Text)[] texts)
    {
        Dictionary<PortName, string> values = new() { [new PortName("任务路径")] = Root };
        foreach ((string port, string text) in texts)
        {
            values[new PortName(port)] = text;
        }

        return new ExecutionInput
        {
            Task = new TaskId(1),
            Node = TargetPath,
            Type = CommandNodeTypes.Create(parameters, process),
            ExecutionCount = 1,
            Texts = values,
        };
    }

    private static CommandParameters Parameters(
        int maxChars = CommandNodeTypes.DefaultMaxChars,
        string? directory = null,
        bool failOnNonZeroExitCode = false,
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

        public Task<ProcessResult> RunAsync(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Run(program, arguments, workingDirectory, timeout));
        }
    }
}