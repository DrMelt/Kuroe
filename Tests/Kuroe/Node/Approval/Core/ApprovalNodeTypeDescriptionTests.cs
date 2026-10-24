using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Node.Approval;
using Xunit;

namespace Kuroe.Node.Approval.Tests;

/// <summary>审批节点的描述契约：字段表与提问文本的读取。</summary>
public sealed class ApprovalNodeTypeDescriptionTests
{
    private static readonly StubInputChannel Channel = new();
    private static readonly ApprovalNodeTypeDescription File = new(Channel);

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

    private static ApprovalParameters Declared(INodeType type) => Assert.IsType<ApprovalNodeType>(type).Parameters!;

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(string? question = null) : IParameterReader
    {
        public string? Text(string name) => name == "Question" ? question : null;

        public IReadOnlyList<string> Texts(string name) => [];

        public int? Integer(string name) => null;
    }
}