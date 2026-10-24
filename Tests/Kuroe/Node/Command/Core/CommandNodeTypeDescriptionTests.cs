using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Process;
using Xunit;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令节点的描述契约：字段表、参数读取与执行目录边界校验。</summary>
public sealed class CommandNodeTypeDescriptionTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuroe-command-description-tests"));

    [Fact]
    public void Description_declares_the_kind_shape_and_fields()
    {
        CommandNodeTypeDescription description = new(Root, new FakeProcessRunner());

        Assert.Equal(CommandNodeTypes.Name, description.Kind);
        Assert.Equal(CommandNodeTypes.Shape, description.Shape);
        Assert.Equal(
            [
                new ParameterField("Program", ParameterValueKind.Text),
                new ParameterField("Arguments", ParameterValueKind.Texts),
                new ParameterField("Directory", ParameterValueKind.Text),
                new ParameterField("TimeoutSeconds", ParameterValueKind.Integer),
                new ParameterField("MaxChars", ParameterValueKind.Integer),
            ],
            description.Fields);
    }

    [Fact]
    public void Read_keeps_program_arguments_directory_and_limits()
    {
        List<string> failures = [];
        INodeType type = new CommandNodeTypeDescription(Root, new FakeProcessRunner())
            .Read(new Reader("dotnet", ["build", "--no-restore"], "src", 30, 1200), failures.Add);

        CommandParameters parameters = Assert.IsType<CommandNodeType>(type).Parameters!;

        Assert.Empty(failures);
        Assert.Equal("dotnet", parameters.Program);
        Assert.Equal(["build", "--no-restore"], parameters.Arguments);
        Assert.Equal(Path.Combine(Root, "src"), parameters.FullDirectory);
        Assert.Equal(30, parameters.TimeoutSeconds);
        Assert.Equal(1200, parameters.MaxChars);
    }

    [Fact]
    public void Missing_program_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, new FakeProcessRunner()).Read(new Reader(), failures.Add);

        Assert.Equal(["命令执行节点必须声明 Program。"], failures);
    }

    [Fact]
    public void Directory_outside_the_working_directory_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, new FakeProcessRunner())
            .Read(new Reader("dotnet", directory: "../escape"), failures.Add);

        Assert.Equal([$"Directory ../escape 不是工作目录内的合法路径。"], failures);
    }

    [Fact]
    public void Non_positive_timeout_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, new FakeProcessRunner())
            .Read(new Reader("dotnet", timeoutSeconds: 0), failures.Add);

        Assert.Equal(["TimeoutSeconds 必须是正整数。"], failures);
    }

    [Fact]
    public void Negative_max_chars_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, new FakeProcessRunner())
            .Read(new Reader("dotnet", maxChars: -1), failures.Add);

        Assert.Equal(["MaxChars 不能是负数。"], failures);
    }

    [Fact]
    public void Defaults_use_the_working_directory_and_declared_limits()
    {
        List<string> failures = [];
        INodeType type = new CommandNodeTypeDescription(Root, new FakeProcessRunner())
            .Read(new Reader("dotnet"), failures.Add);

        CommandParameters parameters = Assert.IsType<CommandNodeType>(type).Parameters!;

        Assert.Empty(failures);
        Assert.Equal(Root, parameters.FullDirectory);
        Assert.Equal(CommandNodeTypes.DefaultTimeoutSeconds, parameters.TimeoutSeconds);
        Assert.Equal(CommandNodeTypes.DefaultMaxChars, parameters.MaxChars);
    }

    [Fact]
    public void Registration_adds_the_command_kind()
    {
        RecordingRegistry registry = new();

        CommandNodeTypeDescriptions.RegisterInto(registry, Root, new FakeProcessRunner());

        Assert.Equal([CommandNodeTypes.Name], registry.Kinds);
    }

    /// <summary>测试用的参数段取值：只给出命令节点的字段。</summary>
    private sealed class Reader(
        string? program = null,
        IReadOnlyList<string>? arguments = null,
        string? directory = null,
        int? timeoutSeconds = null,
        int? maxChars = null) : IParameterReader
    {
        public string? Text(string name) => name switch
        {
            "Program" => program,
            "Directory" => directory,
            _ => null,
        };

        public IReadOnlyList<string> Texts(string name) => name == "Arguments" ? arguments ?? [] : [];

        public int? Integer(string name) => name switch
        {
            "TimeoutSeconds" => timeoutSeconds,
            "MaxChars" => maxChars,
            _ => null,
        };
    }

    /// <summary>测试用的描述注册表：记录注册序并按名查找。</summary>
    private sealed class RecordingRegistry : INodeTypeDescriptionRegistry
    {
        private readonly List<INodeTypeDescription> _descriptions = [];

        public void Register(INodeTypeDescription description) => _descriptions.Add(description);

        public INodeTypeDescription? Find(NodeKind kind) =>
            _descriptions.FirstOrDefault(description => description.Kind == kind);

        public INodeTypeDescription? Find(string name) =>
            _descriptions.FirstOrDefault(description => description.Kind.Value.Equals(name, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<NodeKind> Kinds => [.. _descriptions.Select(description => description.Kind)];
    }

    /// <summary>测试用的进程执行替身，描述读取阶段不会调用。</summary>
    private sealed class FakeProcessRunner : IProcessRunner
    {
        public ProcessResult Run(
            string program,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            TimeSpan timeout) =>
            new(string.Empty, 0, TimedOut: false);
    }
}