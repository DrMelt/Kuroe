using Kuroe.FlowDescription.Shared.Parameters;
using Kuroe.FlowDescription.Shared.Compilation.NodeTypes;
using Kuroe.FlowDescription.Shared.Model;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Xunit;

namespace Kuroe.Node.Command.Tests;

/// <summary>命令节点的描述契约：字段表与参数读取。</summary>
public sealed class CommandNodeTypeDescriptionTests
{
    private static readonly IExecutionRunnerFactory RunnerFactory = new UnusedRunnerFactory();

    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "kuroe-command-description-tests"));

    [Fact(DisplayName = "描述声明种类形状与字段")]
    public void Description_declares_the_kind_shape_and_fields()
    {
        CommandNodeTypeDescription description = new(Root, RunnerFactory);

        Assert.Equal(CommandNodeTypes.Name, description.Kind);
        Assert.Equal(CommandNodeTypes.Shape, description.Shape);
        Assert.Equal(
            [
                new ParameterField("Program", ParameterValueKind.Text),
                new ParameterField("Arguments", ParameterValueKind.Texts),
                new ParameterField("Directory", ParameterValueKind.Text),
                new ParameterField("Variables", ParameterValueKind.Map),
                new ParameterField("TimeoutSeconds", ParameterValueKind.Integer),
                new ParameterField("MaxChars", ParameterValueKind.Integer),
                new ParameterField("FailOnNonZeroExitCode", ParameterValueKind.Boolean),
            ],
            description.Fields);
    }

    [Fact(DisplayName = "读取保留程序实参目录与上限")]
    public void Read_keeps_program_arguments_directory_and_limits()
    {
        List<string> failures = [];
        INodeType type = new CommandNodeTypeDescription(Root, RunnerFactory)
            .Read(new Reader("dotnet", ["build", "--no-restore"], "{root}/src", 30, 1200,
                new Dictionary<string, string> { ["root"] = "任务路径" }, failOnNonZeroExitCode: true), failures.Add);

        CommandExecutionParameters parameters =
            Assert.IsType<CommandExecutionParameters>(Assert.IsType<CommandNodeType>(type).Parameters);

        Assert.Empty(failures);
        Assert.Equal("dotnet", parameters.Program);
        Assert.Equal(["build", "--no-restore"], parameters.Arguments);
        Assert.Equal("{root}/src", parameters.Directory);
        Assert.Equal(new Dictionary<string, string> { ["root"] = "任务路径" }, parameters.Variables);
        Assert.Equal(Root, parameters.RootDirectory);
        Assert.Equal(30, parameters.TimeoutSeconds);
        Assert.Equal(1200, parameters.MaxChars);
        Assert.True(parameters.FailOnNonZeroExitCode);
    }

    [Fact(DisplayName = "缺少程序被报告")]
    public void Missing_program_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, RunnerFactory).Read(new Reader(), failures.Add);

        Assert.Equal(["命令执行节点必须声明 Program。"], failures);
    }

    [Fact(DisplayName = "非正超时被报告")]
    public void Non_positive_timeout_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, RunnerFactory)
            .Read(new Reader("dotnet", timeoutSeconds: 0), failures.Add);

        Assert.Equal(["TimeoutSeconds 必须是正整数。"], failures);
    }

    [Fact(DisplayName = "命令节点负数最大字符数被报告")]
    public void Negative_max_chars_is_reported()
    {
        List<string> failures = [];
        _ = new CommandNodeTypeDescription(Root, RunnerFactory)
            .Read(new Reader("dotnet", maxChars: -1), failures.Add);

        Assert.Equal(["MaxChars 不能是负数。"], failures);
    }

    [Fact(DisplayName = "默认值使用工作区根与声明上限")]
    public void Defaults_use_the_workspace_root_and_declared_limits()
    {
        List<string> failures = [];
        INodeType type = new CommandNodeTypeDescription(Root, RunnerFactory)
            .Read(new Reader("dotnet"), failures.Add);

        CommandExecutionParameters parameters =
            Assert.IsType<CommandExecutionParameters>(Assert.IsType<CommandNodeType>(type).Parameters);

        Assert.Empty(failures);
        Assert.Null(parameters.Directory);
        Assert.Equal(Root, parameters.RootDirectory);
        Assert.Equal(CommandNodeTypes.DefaultTimeoutSeconds, parameters.TimeoutSeconds);
        Assert.Equal(CommandNodeTypes.DefaultMaxChars, parameters.MaxChars);
        Assert.False(parameters.FailOnNonZeroExitCode);
    }

    [Fact(DisplayName = "注册加入命令节点种类")]
    public void Registration_adds_the_command_kind()
    {
        RecordingRegistry registry = new();

        CommandNodeTypeDescriptions.RegisterInto(registry, Root, RunnerFactory);

        Assert.Equal([CommandNodeTypes.Name], registry.Kinds);
    }

    /// <summary>测试用的参数段取值：只给出命令节点的字段。</summary>
    private sealed class Reader(
        string? program = null,
        IReadOnlyList<string>? arguments = null,
        string? directory = null,
        int? timeoutSeconds = null,
        int? maxChars = null,
        IReadOnlyDictionary<string, string>? variables = null,
        bool? failOnNonZeroExitCode = null) : IParameterReader
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

        public bool? Boolean(string name) =>
            name == "FailOnNonZeroExitCode" ? failOnNonZeroExitCode : null;

        public IReadOnlyDictionary<string, string> Mappings(string name) =>
            name == "Variables" ? variables ?? new Dictionary<string, string>() : new Dictionary<string, string>();

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> MappingMaps(string name) =>
            new Dictionary<string, IReadOnlyDictionary<string, string>>();
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

    private sealed class UnusedRunnerFactory : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            throw new InvalidOperationException("测试节点类型不建立运行器。");
    }
}
