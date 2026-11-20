using Kuroe.FlowDescription.Shared.Parameters;
using Kuroe.FlowDescription.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Node.Approval.Tests;

/// <summary>审批节点的描述契约：字段表与提问文本的读取。</summary>
public sealed class ApprovalNodeTypeDescriptionTests
{
    private static readonly ApprovalNodeTypeDescription File = new();

    [Fact(DisplayName = "种类是审批节点类型")]
    public void Kind_is_the_approval_node_type() =>
        Assert.Equal(ApprovalNodeTypes.Name, File.Kind);

    [Fact(DisplayName = "形状是审批类型形状")]
    public void Shape_is_the_approval_type_shape() =>
        Assert.Equal(ApprovalNodeTypes.Shape, File.Shape);

    [Fact(DisplayName = "审批类型字段声明问题文本")]
    public void Fields_declare_the_question_text() =>
        Assert.Equal([new ParameterField("Question", ParameterValueKind.Text)], File.Fields);

    [Fact(DisplayName = "审批类型读取从值构建")]
    public void Read_builds_the_type_from_the_values()
    {
        ApprovalParameters parameters = Declared(File.Read(new Reader("确认计划"), _ => { }));

        Assert.Equal("确认计划", parameters.Question);
    }

    [Fact(DisplayName = "审批类型没有值时读取采用安全默认值")]
    public void Read_without_values_takes_the_safe_defaults()
    {
        ApprovalParameters parameters = Declared(File.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Question);
    }

    private static ApprovalParameters Declared(INodeType type) =>
        Assert.IsType<ApprovalParameters>(Assert.IsType<ApprovalNodeType>(type).Parameters);

    /// <summary>测试用的参数段取值：按字段名给出本轮内容，未给的字段取安全默认。</summary>
    private sealed class Reader(string? question = null) : IParameterReader
    {
        public string? Text(string name) => name == "Question" ? question : null;

        public IReadOnlyList<string> Texts(string name) => [];

        public int? Integer(string name) => null;

        public bool? Boolean(string name) => null;

        public IReadOnlyDictionary<string, string> Mappings(string name) => new Dictionary<string, string>();

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> MappingMaps(string name) =>
            new Dictionary<string, IReadOnlyDictionary<string, string>>();
    }


}
