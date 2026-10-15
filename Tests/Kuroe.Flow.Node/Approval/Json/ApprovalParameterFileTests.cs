using Kuroe.Flow.Json.Parameters;
using Xunit;

namespace Kuroe.Flow.Node.Approval.Json.Tests;

/// <summary>审批节点参数段的文件形状：字段表与提问文本的读写。</summary>
public sealed class ApprovalParameterFileTests
{
    private static readonly ApprovalParameterFile Shape = new();

    [Fact]
    public void Kind_is_the_approval_node_type() =>
        Assert.Equal(ApprovalNodeTypes.Name, Shape.Kind);

    [Fact]
    public void Fields_declare_the_question_text() =>
        Assert.Equal([new ParameterField("Question", ParameterValueKind.Text)], Shape.Fields);

    [Fact]
    public void Read_builds_the_parameters_from_the_values()
    {
        ApprovalParameters parameters = Assert.IsType<ApprovalParameters>(Shape.Read(new Reader("确认计划"), _ => { }));

        Assert.Equal("确认计划", parameters.Question);
    }

    [Fact]
    public void Read_without_values_takes_the_safe_defaults()
    {
        ApprovalParameters parameters = Assert.IsType<ApprovalParameters>(Shape.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Question);
    }

    [Fact]
    public void Write_keeps_only_the_written_fields()
    {
        Assert.Empty(Shape.Write(new ApprovalParameters()).Values);
        Assert.Empty(Shape.Write(null).Values);

        ParameterValues values = Shape.Write(new ApprovalParameters { Question = "确认计划" });

        Assert.Equal(
            [("Question", ParameterValueKind.Text)],
            values.Values.Select(value => (value.Name, value.Kind)));
        Assert.Equal("确认计划", values.Values[0].Text);
    }

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(string? question = null) : IParameterReader
    {
        public string? Text(string name) => name == "Question" ? question : null;

        public IReadOnlyList<string> Texts(string name) => [];
    }
}