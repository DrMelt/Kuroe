using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Xunit;

namespace Kuroe.Node.Input.Json.Tests;

/// <summary>输入节点的文件形状：字段表与提问文本的读写。</summary>
public sealed class InputTypeFileTests
{
    private static readonly StubInputChannel Channel = new();
    private static readonly InputTypeFile File = new(Channel);

    [Fact]
    public void Kind_is_the_input_node_type() =>
        Assert.Equal(InputNodeTypes.Name, File.Kind);

    [Fact]
    public void Shape_is_the_input_type_shape() =>
        Assert.Equal(InputNodeTypes.Shape, File.Shape);

    [Fact]
    public void Fields_declare_the_question_text() =>
        Assert.Equal([new ParameterField("Question", ParameterValueKind.Text)], File.Fields);

    [Fact]
    public void Read_builds_the_type_from_the_values()
    {
        InputParameters parameters = Declared(File.Read(new Reader("要做什么"), _ => { }));

        Assert.Equal("要做什么", parameters.Question);
    }

    [Fact]
    public void Read_without_values_takes_the_safe_defaults()
    {
        InputParameters parameters = Declared(File.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Question);
    }

    [Fact]
    public void Write_keeps_only_the_written_fields()
    {
        Assert.Empty(File.Write(InputNodeTypes.Create(new InputParameters(), Channel)).Values);
        Assert.Empty(File.Write(null).Values);

        ParameterValues values = File.Write(InputNodeTypes.Create(new InputParameters { Question = "要做什么" }, Channel));

        Assert.Equal(
            [("Question", ParameterValueKind.Text)],
            values.Values.Select(value => (value.Name, value.Kind)));
        Assert.Equal("要做什么", values.Values[0].Text);
    }

    private static InputParameters Declared(INodeType type) => Assert.IsType<InputNodeType>(type).Parameters!;

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(string? question = null) : IParameterReader
    {
        public string? Text(string name) => name == "Question" ? question : null;

        public IReadOnlyList<string> Texts(string name) => [];
    }
}