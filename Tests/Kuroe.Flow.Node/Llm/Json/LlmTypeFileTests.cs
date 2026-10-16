using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Flow.Node.Llm.Json.Tests;

/// <summary>Llm 节点的文件形状：字段表、能力路径与系统指令块的规则、类型实例的读写。</summary>
public sealed class LlmTypeFileTests
{
    private const string PromptName = "Prompt";
    private const string ToolsName = "Tools";
    private const string SystemPromptName = "SystemPrompt";

    private static readonly LlmTypeFile File = new();

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
            new Reader("围绕目标展开", ["ReadFile", "files/WriteFile"], ["恒定块"]),
            _ => { }));

        Assert.Equal("围绕目标展开", parameters.Prompt);
        Assert.Equal(["ReadFile", "files/WriteFile"], parameters.Tools.Select(tool => tool.Value));
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
        File.Read(new Reader(tools: ["files/写入 文件"]), errors.Add);

        Assert.Equal(["路径 files/写入 文件 的段 写入 文件 不能为空或含空白与花括号。"], errors);
    }

    [Fact]
    public void Read_reports_a_blank_system_block()
    {
        List<string> errors = [];
        File.Read(new Reader(blocks: ["恒定块", " "]), errors.Add);

        Assert.Equal(["系统指令块不能为空。"], errors);
    }

    [Fact]
    public void Write_keeps_only_the_written_fields()
    {
        Assert.Empty(File.Write(LlmNodeTypes.Create(new LlmParameters())).Values);
        Assert.Empty(File.Write(null).Values);

        ParameterValues values = File.Write(LlmNodeTypes.Create(new LlmParameters
        {
            Prompt = "围绕目标展开",
            Tools = [new ToolPath("ReadFile")],
        }));

        Assert.Equal(
            [(PromptName, ParameterValueKind.Text), (ToolsName, ParameterValueKind.Texts)],
            values.Values.Select(value => (value.Name, value.Kind)));
        Assert.Equal("围绕目标展开", values.Values[0].Text);
        Assert.Equal(["ReadFile"], values.Values[1].Texts);
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
    }
}