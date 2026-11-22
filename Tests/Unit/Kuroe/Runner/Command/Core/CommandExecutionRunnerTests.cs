using ErrorOr;
using Kuroe.Process;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Runner.Command.Tests;

/// <summary>命令执行器：按命令变量执行进程，成功交回合并输出，失败交回原因。</summary>
public sealed class CommandExecutionRunnerTests
{
    private static readonly string Root = Path.GetTempPath();

    [Fact(DisplayName = "执行返回去空白输出")]
    public async Task Execute_returns_the_trimmed_output()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns($"甲{Environment.NewLine}乙{Environment.NewLine}");
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(ExecutionOutputKind.Content, result.Value.Kind);
        Assert.Equal($"甲{Environment.NewLine}乙", result.Value.Content);
    }

    [Fact(DisplayName = "非零退出码默认视为成功")]
    public async Task Non_zero_exit_code_succeeds_by_default()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("错误", result.Value.Content);
    }

    [Fact(DisplayName = "声明时非零退出码视为失败")]
    public async Task Non_zero_exit_code_fails_when_declared()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("错误", exitCode: 2);
        CommandExecutionParameters parameters = Parameters(failOnNonZeroExitCode: true);
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("退出码 2", result.FirstError.Description);
        Assert.Contains("错误", result.FirstError.Description);
    }

    [Fact(DisplayName = "空输出以空内容成功")]
    public async Task Empty_output_succeeds_with_empty_content()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns(string.Empty, exitCode: 1);
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(string.Empty, result.Value.Content);
    }

    [Fact(DisplayName = "超时视为失败")]
    public async Task Timeout_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("部分输出", timedOut: true);
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("命令在 5 秒内未结束，已终止。", result.FirstError.Description);
    }

    [Fact(DisplayName = "启动失败视为失败")]
    public async Task Start_failure_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Throws("无法启动 dotnet。");
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("无法启动 dotnet。", result.FirstError.Description);
    }

    [Fact(DisplayName = "执行传递程序实参目录与超时")]
    public async Task Execute_passes_program_arguments_directory_and_timeout()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(parameters, process);

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal("dotnet", process.Program);
        Assert.Equal(["--version"], process.Arguments);
        Assert.Equal(Root, process.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(5), process.Timeout);
    }

    [Fact(DisplayName = "实参展开变量但不校验路径")]
    public async Task Arguments_expand_variables_without_path_validation()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(
            arguments: ["clone", "--branch", "{分支名}", "{仓库地址}", "."],
            variables: new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["仓库地址"] = "仓库地址",
                ["分支名"] = "分支名",
            });
        CommandExecutionRunner runner = Runner(
            parameters,
            process,
            Input(
                ("任务路径", Root),
                ("仓库地址", @"D:\outside\repo.git"),
                ("分支名", "feature/x")));

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["clone", "--branch", "feature/x", @"D:\outside\repo.git", "."], process.Arguments);
        Assert.Equal(Root, process.WorkingDirectory);
    }

    [Fact(DisplayName = "未映射的实参占位符在启动前失败")]
    public async Task Unmapped_argument_placeholder_fails_without_starting()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(arguments: ["clone", "{missing}"]);
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("含未映射的变量 missing", result.FirstError.Description);
        Assert.Equal(0, process.Calls);
    }

    [Fact(DisplayName = "执行把根变量作为默认目录")]
    public async Task Execute_uses_the_root_variable_as_the_default_directory()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(
            parameters,
            process,
            Input(("任务路径", Path.Combine(Root, "sub"))));

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact(DisplayName = "执行从根变量解析声明目录")]
    public async Task Execute_resolves_the_declared_directory_from_the_root_variable()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(directory: "sub");
        CommandExecutionRunner runner = Runner(parameters, process, Input(("任务路径", Root)));

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact(DisplayName = "工作区之外的目录失败")]
    public async Task Directory_outside_the_workspace_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(directory: "../escape");
        CommandExecutionRunner runner = Runner(parameters, process, Input(("任务路径", Root)));

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Directory {root}/../escape 不是工作区内的合法路径", result.FirstError.Description);
        Assert.Equal(0, process.Calls);
    }

    [Fact(DisplayName = "展开目录前解析相对根")]
    public async Task Relative_root_is_resolved_before_expanding_the_directory()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(directoryTemplate: "{root}");
        CommandExecutionRunner runner = Runner(parameters, process, Input(("任务路径", "sub")));

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub"), process.WorkingDirectory);
    }

    [Fact(DisplayName = "相对根是目录模板的基准")]
    public async Task Relative_root_is_the_base_of_the_directory_template()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(directoryTemplate: "{root}/src");
        CommandExecutionRunner runner = Runner(parameters, process, Input(("任务路径", "sub")));

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "sub", "src"), process.WorkingDirectory);
    }

    [Fact(DisplayName = "目录变量相对根解析")]
    public async Task Directory_variables_are_relative_to_the_root()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters(
            directoryTemplate: "{root}/{sub}",
            variables: new Dictionary<string, string>
            {
                ["root"] = "任务路径",
                ["sub"] = "子目录",
            });
        CommandExecutionRunner runner = Runner(
            parameters,
            process,
            Input(("任务路径", "task"), ("子目录", "src")));

        await runner.ExecuteAsync(CancellationToken.None);

        Assert.Equal(Path.Combine(Root, "task", "src"), process.WorkingDirectory);
    }

    [Fact(DisplayName = "命令执行变量根在工作区之外失败")]
    public async Task Variable_root_outside_the_workspace_fails()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("ok");
        CommandExecutionParameters parameters = Parameters();
        CommandExecutionRunner runner = Runner(
            parameters,
            process,
            Input(("任务路径", Path.GetFullPath("..", Root))));

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("不是工作区内的合法路径", result.FirstError.Description);
        Assert.Equal(0, process.Calls);
    }

    [Fact(DisplayName = "输出按声明上限截断")]
    public async Task Output_is_truncated_at_the_declared_limit()
    {
        FakeProcessRunner process = new FakeProcessRunner().Returns("abcdef");
        CommandExecutionParameters parameters = Parameters(maxChars: 3);
        CommandExecutionRunner runner = Runner(parameters, process);

        ErrorOr<ExecutionOutput> result = await runner.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("abc…输出已截断", result.Value.Content);
    }

    private static CommandExecutionRunner Runner(
        CommandExecutionParameters parameters,
        FakeProcessRunner process,
        ExecutionRequest? request = null) =>
        new(parameters, process, new Entry(request ?? Input()), TimeProvider.System);

    private static ExecutionRequest Input(
        params (string Port, string Text)[] texts)
    {
        Dictionary<ExecutionPortName, string> values = new() { [new ExecutionPortName("任务路径")] = Root };
        foreach ((string port, string text) in texts)
        {
            values[new ExecutionPortName(port)] = text;
        }

        return new ExecutionRequest(
            new ExecutionOwnerId("1"),
            new ExecutionStepId("整体/节点"),
            [],
            [],
            values,
            []);
    }

    private static CommandExecutionParameters Parameters(
        int maxChars = 8000,
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

    private sealed class Entry(ExecutionRequest request) : IExecutionRequestEntry
    {
        public ExecutionId Id => new(1);

        public ExecutionRequest Request => request;

        public ExecutionStatus ReadStatus() => throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
            throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
            throw new NotSupportedException();
    }

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
