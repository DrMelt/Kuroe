using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>工具路径与路径变量的装载期规则：映射落在白名单内并覆盖每个可用工具的全部变量。</summary>
public sealed class ToolPortRulesTests
{
    private static readonly ToolContract Build = new("build/Build", ["root"]);
    private static readonly ToolContract Read = new("file/read/ReadFile", ["root"]);
    private static readonly ToolContract Write = new("file/write/WriteFile", ["project"]);
    private static readonly ToolContract Info = new("KuroeInfo/GetTask", []);

    private static readonly IToolContractCatalog Catalog = new FakeCatalog(Build, Read, Write, Info);

    [Fact]
    public void Prefix_mapping_covers_every_whitelisted_tool()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("file/read")],
            ToolPorts = Mapping(("file", ("root", "任务路径"))),
        };

        Assert.Empty(Failures(parameters, Text("任务路径")));
    }

    [Fact]
    public void Missing_mapping_for_a_whitelisted_variable_tool_is_rejected()
    {
        LlmParameters parameters = new() { Tools = [new ToolPath("file/read")] };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("工具 file/read/ReadFile 缺少路径变量 root 的映射", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_variable_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["build"] = new Dictionary<string, string>
                {
                    ["root"] = "任务路径",
                    ["extra"] = "任务路径",
                },
            },
        };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("路径变量 extra 不属于工具定义", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_variable_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["build"] = new Dictionary<string, string>(),
            },
        };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("工具 build/Build 缺少路径变量 root 的映射", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_tool_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = Mapping(("missing", ("root", "任务路径"))),
        };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("工具路径 missing 没有匹配任何已注册工具", StringComparison.Ordinal));
    }

    [Fact]
    public void Mapping_outside_the_whitelist_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("file/read")],
            ToolPorts = Mapping(("file/write", ("project", "项目路径"))),
        };

        Assert.Contains(Failures(parameters, Text("任务路径"), Text("项目路径")),
            error => error.Contains("工具路径 file/write 不在工具白名单内", StringComparison.Ordinal));
    }

    [Fact]
    public void Mapping_a_tool_without_variables_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("KuroeInfo")],
            ToolPorts = Mapping(("KuroeInfo", ("root", "任务路径"))),
        };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("工具 KuroeInfo/GetTask 没有路径变量，不能写映射", StringComparison.Ordinal));
    }

    [Fact]
    public void Longest_prefix_wins_and_specific_mapping_overrides_the_parent()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("file")],
            ToolPorts = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["file"] = new Dictionary<string, string> { ["root"] = "任务路径" },
                ["file/write"] = new Dictionary<string, string> { ["project"] = "项目路径" },
            },
        };

        Assert.Empty(Failures(parameters, Text("任务路径"), Text("项目路径")));
    }

    [Fact]
    public void A_mapped_port_must_be_text()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = Mapping(("build", ("root", "任务路径"))),
        };

        Assert.Contains(Failures(parameters, new InputPort(new PortName("任务路径"), PortTypes.Signal)),
            error => error.Contains("输入端口 任务路径 必须是 Text", StringComparison.Ordinal));
    }

    [Fact]
    public void A_mapped_port_must_be_declared()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = Mapping(("build", ("root", "任务路径"))),
        };

        Assert.Contains(Failures(parameters),
            error => error.Contains("映射到未声明的输入端口 任务路径", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_mapping_key_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = Mapping(("", ("root", "任务路径"))),
        };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("工具路径不能为空", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_variable_name_is_rejected()
    {
        LlmParameters parameters = new()
        {
            Tools = [new ToolPath("build")],
            ToolPorts = Mapping(("build", ("", "任务路径"))),
        };

        Assert.Contains(Failures(parameters, Text("任务路径")),
            error => error.Contains("路径变量名不能为空", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> Failures(
        LlmParameters parameters,
        params InputPort[] inputs)
    {
        LlmNodeType type = new(parameters, Catalog);
        NodeSpec node = new()
        {
            Label = new NodeLabel("干活"),
            Type = type,
            Inputs = inputs,
            Outputs = [new Port(new PortName("结论"), PortTypes.Text)],
            Spec = new ExecutableSpec(),
        };

        List<Error> errors = [];
        type.Validate(node, NodeScope.Flow("测试"), errors.Add);

        return [.. errors.Select(error => error.Description)];
    }

    private static InputPort Text(string name) => new(new PortName(name), PortTypes.Text);

    private static Dictionary<string, IReadOnlyDictionary<string, string>> Mapping(
        params (string Path, (string Variable, string Port) Port)[] entries) =>
        entries.ToDictionary(
            entry => entry.Path,
            entry => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
            {
                [entry.Port.Variable] = entry.Port.Port,
            });

    private sealed class FakeCatalog(params ToolContract[] contracts) : IToolContractCatalog
    {
        public IReadOnlyList<ToolContract> Contracts { get; } = contracts;
    }
}