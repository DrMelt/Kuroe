using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flow.Node.Approval.Json.Tests;

/// <summary>审批节点的文件形状：字段表与提问文本的读写。</summary>
public sealed class ApprovalTypeFileTests
{
    private static readonly ApprovalTypeFile File = new();

    [Fact]
    public void Kind_is_the_approval_node_type() =>
        Assert.Equal(ApprovalNodeTypes.Name, File.Kind);

    [Fact]
    public void Shape_is_the_approval_type_shape() =>
        Assert.Equal(ApprovalNodeTypes.Shape, File.Shape);

    [Fact]
    public void Fields_declare_the_question_text() =>
        Assert.Equal([new ParameterField("Question", ParameterValueKind.Text)], File.Fields);

    [Fact]
    public void Read_builds_the_type_from_the_values()
    {
        ApprovalParameters parameters = Declared(File.Read(new Reader("确认计划"), _ => { }));

        Assert.Equal("确认计划", parameters.Question);
    }

    [Fact]
    public void Read_without_values_takes_the_safe_defaults()
    {
        ApprovalParameters parameters = Declared(File.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Question);
    }

    [Fact]
    public void Write_keeps_only_the_written_fields()
    {
        Assert.Empty(File.Write(ApprovalNodeTypes.Create(new ApprovalParameters())).Values);
        Assert.Empty(File.Write(null).Values);

        ParameterValues values = File.Write(ApprovalNodeTypes.Create(new ApprovalParameters { Question = "确认计划" }));

        Assert.Equal(
            [("Question", ParameterValueKind.Text)],
            values.Values.Select(value => (value.Name, value.Kind)));
        Assert.Equal("确认计划", values.Values[0].Text);
    }

    private static ApprovalParameters Declared(INodeType type) => Assert.IsType<ApprovalNodeType>(type).Parameters!;

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(string? question = null) : IParameterReader
    {
        public string? Text(string name) => name == "Question" ? question : null;

        public IReadOnlyList<string> Texts(string name) => [];
    }
}