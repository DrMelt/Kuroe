using ErrorOr;
using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Shared;
using Xunit;

namespace Kuroe.Tool.Command.Tests;

/// <summary>命令执行编排：进程由替身给出结果，全程不起进程。</summary>
public sealed class CommandToolRunnerTests
{
    [Fact]
    public void Execute_returns_merged_output_with_exit_code()
    {
        FakeProcessRunner process = new()
        {
            Result = new CommandProcessResult($"甲{Environment.NewLine}乙{Environment.NewLine}", 0, false),
        };

        ErrorOr<string> result = Invoke(Definition(), Root(), Given(), process);

        Assert.False(result.IsError);
        Assert.Equal($"甲{Environment.NewLine}乙\n（退出码 0）", result.Value);
    }

    [Fact]
    public void Execute_truncates_output_at_definition_limit()
    {
        FakeProcessRunner process = new()
        {
            Result = new CommandProcessResult("abcdefgh", 1, false),
        };

        ErrorOr<string> result = Invoke(Definition(outputLimit: 5), Root(), Given(), process);

        Assert.False(result.IsError);
        Assert.Equal("abcde…输出已截断\n（退出码 1）", result.Value);
    }

    [Fact]
    public void Execute_reports_empty_output_with_exit_code()
    {
        FakeProcessRunner process = new()
        {
            Result = new CommandProcessResult(string.Empty, 3, false),
        };

        ErrorOr<string> result = Invoke(Definition(), Root(), Given(), process);

        Assert.False(result.IsError);
        Assert.Equal("命令已结束，退出码 3，无输出。", result.Value);
    }

    [Fact]
    public void Execute_reports_timeout_as_failure()
    {
        FakeProcessRunner process = new()
        {
            Result = new CommandProcessResult("部分输出", 0, true),
        };

        ErrorOr<string> result = Invoke(Definition(timeoutSeconds: 5), Root(), Given(), process);

        Assert.True(result.IsError);
        Assert.Equal(ToolErrorCodes.Execute, result.FirstError.Code);
        Assert.Equal("命令在 5 秒内未结束，已终止。", result.FirstError.Description);
    }

    [Fact]
    public void Execute_propagates_process_failure()
    {
        FakeProcessRunner process = new()
        {
            Failure = ToolErrors.Execute("无法启动 cmd。"),
        };

        ErrorOr<string> result = Invoke(Definition(), Root(), Given(), process);

        Assert.True(result.IsError);
        Assert.Equal(ToolErrorCodes.Execute, result.FirstError.Code);
        Assert.Equal("无法启动 cmd。", result.FirstError.Description);
    }

    [Fact]
    public void Execute_passes_expanded_arguments_working_directory_and_timeout()
    {
        string root = Root();
        CommandToolDefinition definition = new(
            new ToolPath("Execution"),
            "说明",
            [],
            [new("cmd", null), new("echo", null), new("甲", null)],
            "src",
            7,
            200);
        FakeProcessRunner process = new();

        ErrorOr<string> result = Invoke(definition, root, Given(), process);

        Assert.False(result.IsError);
        Assert.Equal("cmd", process.Program);
        Assert.Equal(["echo", "甲"], process.Arguments);
        Assert.Equal(Path.GetFullPath("src", root), process.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(7), process.Timeout);
    }

    [Fact]
    public void Execute_clamps_timeout_to_maximum()
    {
        FakeProcessRunner process = new();

        ErrorOr<string> result = Invoke(Definition(timeoutSeconds: int.MaxValue), Root(), Given(), process);

        Assert.False(result.IsError);
        Assert.Equal(TimeSpan.FromSeconds(CommandToolTemplate.MaxTimeoutSeconds), process.Timeout);
    }

    [Fact]
    public void Execute_rejects_path_outside_working_directory_before_starting_process()
    {
        CommandToolDefinition definition = PathDefinition(new ToolParameter(new ToolName("path"), "路径", Path: true));
        FakeProcessRunner process = new();

        ErrorOr<string> result = Invoke(
            definition,
            Root(),
            Given(("path", Path.Combine("..", "escape.txt"))),
            process);

        Assert.True(result.IsError);
        Assert.Contains("不在工作目录内", result.FirstError.Description, StringComparison.Ordinal);
        Assert.False(process.Called);
    }

    private static readonly string[] stringArray = ["src/a", "docs/b"];

    [Fact]
    public void Execute_rejects_path_list_item_outside_allow_before_starting_process()
    {
        CommandToolDefinition definition = PathDefinition(
            new ToolParameter(new ToolName("path"), "路径", List: true, Path: true, PathAccess: new ToolPathAccess(["src"], [])));
        FakeProcessRunner process = new();

        ErrorOr<string> result = Invoke(
            definition,
            Root(),
            Given(("path", stringArray)),
            process);

        Assert.True(result.IsError);
        Assert.Contains("docs/b", result.FirstError.Description, StringComparison.Ordinal);
        Assert.Contains("白名单", result.FirstError.Description, StringComparison.Ordinal);
        Assert.False(process.Called);
    }

    /// <summary>临时测试根目录，路径不需要实际存在。</summary>
    private static string Root() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    /// <summary>测试用的命令定义，程序固定，超时与输出上限可调。</summary>
    private static CommandToolDefinition Definition(int timeoutSeconds = 5, int outputLimit = 200) =>
        new(
            new ToolPath("Execution"),
            "说明",
            [],
            [new("cmd", null)],
            null,
            timeoutSeconds,
            outputLimit);

    /// <summary>带路径参数的命令定义，路径校验在启动进程前完成。</summary>
    private static CommandToolDefinition PathDefinition(ToolParameter parameter) =>
        new(
            new ToolPath("Execution"),
            "说明",
            [parameter],
            [new("cmd", null), new("{path}", null)],
            null,
            5,
            200);

    /// <summary>通过工具载体调用，进程由替身执行。</summary>
    private static ErrorOr<string> Invoke(
        CommandToolDefinition definition,
        string root,
        Dictionary<string, object?> values,
        ICommandProcessRunner process) =>
        new CommandTool(definition, root, process).Functions[0].Invoke(new ToolArguments(values));

    /// <summary>一次调用的实参。</summary>
    private static Dictionary<string, object?> Given(params (string Name, object? Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value);

    /// <summary>记录调用并返回预设结果或错误的进程替身。</summary>
    private sealed class FakeProcessRunner : ICommandProcessRunner
    {
        public CommandProcessResult Result { get; init; } = new(string.Empty, 0, false);

        public Error? Failure { get; init; }

        public bool Called { get; private set; }

        public string? Program { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public string? WorkingDirectory { get; private set; }

        public TimeSpan Timeout { get; private set; }

        public ErrorOr<CommandProcessResult> Run(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout)
        {
            Called = true;
            Program = program;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            Timeout = timeout;

            if (Failure is { } failure)
            {
                return failure;
            }

            return Result;
        }
    }
}