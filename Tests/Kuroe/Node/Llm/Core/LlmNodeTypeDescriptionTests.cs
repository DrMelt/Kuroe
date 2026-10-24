using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Node.Llm;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>Llm 节点的描述契约：字段表、能力路径与系统指令块的规则、类型实例的读取。</summary>
public sealed class LlmNodeTypeDescriptionTests
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    private static readonly LlmNodeTypeDescription File = new();

    [Fact]
    public void Kind_is_the_llm_node_type() =>
        Assert.Equal(LlmNodeTypes.Name, File.Kind);

    [Fact]
    public void Shape_is_the_llm_type_shape() =>
        Assert.Equal(LlmNodeTypes.Shape, File.Shape);

    [Fact]
    public void Fields_declare_the_parameter_segment() =>
        Assert.Equal(
            [
                new ParameterField(PromptName, ParameterValueKind.Text),
                new ParameterField(ToolsName, ParameterValueKind.Texts),
                new ParameterField(SystemPromptName, ParameterValueKind.Texts),
            ],
            File.Fields);

    [Fact]
    public void Read_builds_the_type_from_the_values()
    {
        LlmParameters parameters = Declared(File.Read(
            new Reader("围绕目标展开", ["ReadFile", "file/WriteFile"], ["恒定块"]),
            _ => { }));

        Assert.Equal("围绕目标展开", parameters.Prompt);
        Assert.Equal(["ReadFile", "file/WriteFile"], parameters.Tools.Select(tool => tool.Value));
        Assert.Equal(["恒定块"], parameters.SystemPrompt);
    }

    [Fact]
    public void Read_without_values_takes_the_safe_defaults()
    {
        LlmParameters parameters = Declared(File.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Prompt);
        Assert.Empty(parameters.Tools);
        Assert.Empty(parameters.SystemPrompt);
    }

    [Fact]
    public void Read_reports_an_illegal_capability_path()
    {
        List<string> errors = [];
        File.Read(new Reader(tools: ["file/写入 文件"]), errors.Add);

        Assert.Equal(["路径 file/写入 文件 的段 写入 文件 只能写字母、数字、下划线与连字符。"], errors);
    }

    [Fact]
    public void Read_reports_a_blank_system_block()
    {
        List<string> errors = [];
        File.Read(new Reader(blocks: ["恒定块", " "]), errors.Add);

        Assert.Equal(["系统指令块不能为空。"], errors);
    }

    private static LlmParameters Declared(INodeType type) => Assert.IsType<LlmNodeType>(type).Parameters!;

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(
        string? prompt = null,
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? blocks = null) : IParameterReader
    {
        public string? Text(string name) => name == PromptName ? prompt : null;

        public IReadOnlyList<string> Texts(string name) => name switch
        {
            ToolsName => tools ?? [],
            SystemPromptName => blocks ?? [],
            _ => [],
        };

        public int? Integer(string name) => null;
    }
}