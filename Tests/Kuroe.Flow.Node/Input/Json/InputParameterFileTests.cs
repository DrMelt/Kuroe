using Kuroe.Flow.Json.Parameters;
using Xunit;

namespace Kuroe.Flow.Node.Input.Json.Tests;

/// <summary>输入节点参数段的文件形状：字段表与提问文本的读写。</summary>
public sealed class InputParameterFileTests
{
    private static readonly InputParameterFile Shape = new();

    [Fact]
    public void Kind_is_the_input_node_type() =>
        Assert.Equal(InputNodeTypes.Name, Shape.Kind);

    [Fact]
    public void Fields_declare_the_question_text() =>
        Assert.Equal([new ParameterField("Question", ParameterValueKind.Text)], Shape.Fields);

    [Fact]
    public void Read_builds_the_parameters_from_the_values()
    {
        InputParameters parameters = Assert.IsType<InputParameters>(Shape.Read(new Reader("要做什么"), _ => { }));

        Assert.Equal("要做什么", parameters.Question);
    }

    [Fact]
    public void Read_without_values_takes_the_safe_defaults()
    {
        InputParameters parameters = Assert.IsType<InputParameters>(Shape.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Question);
    }

    [Fact]
    public void Write_keeps_only_the_written_fields()
    {
        Assert.Empty(Shape.Write(new InputParameters()).Values);
        Assert.Empty(Shape.Write(null).Values);

        ParameterValues values = Shape.Write(new InputParameters { Question = "要做什么" });

        Assert.Equal(
            [("Question", ParameterValueKind.Text)],
            values.Values.Select(value => (value.Name, value.Kind)));
        Assert.Equal("要做什么", values.Values[0].Text);
    }

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(string? question = null) : IParameterReader
    {
        public string? Text(string name) => name == "Question" ? question : null;

        public IReadOnlyList<string> Texts(string name) => [];
    }
}