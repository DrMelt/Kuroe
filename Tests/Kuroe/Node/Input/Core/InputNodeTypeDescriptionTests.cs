using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Xunit;

namespace Kuroe.Node.Input.Tests;

/// <summary>输入节点的描述契约：字段表与提问文本的读取。</summary>
public sealed class InputNodeTypeDescriptionTests
{
    private static readonly IExecutionRunnerFactory RunnerFactory = new UnusedRunnerFactory();

    private static readonly InputNodeTypeDescription File = new(RunnerFactory);

    [Fact(DisplayName = "种类是输入节点类型")]
    public void Kind_is_the_input_node_type() =>
        Assert.Equal(InputNodeTypes.Name, File.Kind);

    [Fact(DisplayName = "形状是输入类型形状")]
    public void Shape_is_the_input_type_shape() =>
        Assert.Equal(InputNodeTypes.Shape, File.Shape);

    [Fact(DisplayName = "输入类型字段声明问题文本")]
    public void Fields_declare_the_question_text() =>
        Assert.Equal([new ParameterField("Question", ParameterValueKind.Text)], File.Fields);

    [Fact(DisplayName = "输入类型读取从值构建")]
    public void Read_builds_the_type_from_the_values()
    {
        InputParameters parameters = Declared(File.Read(new Reader("要做什么"), _ => { }));

        Assert.Equal("要做什么", parameters.Question);
    }

    [Fact(DisplayName = "输入类型没有值时读取采用安全默认值")]
    public void Read_without_values_takes_the_safe_defaults()
    {
        InputParameters parameters = Declared(File.Read(new Reader(), _ => { }));

        Assert.Null(parameters.Question);
    }

    private static InputParameters Declared(INodeType type) =>
        Assert.IsType<InputParameters>(Assert.IsType<InputNodeType>(type).Parameters);

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

    private sealed class UnusedRunnerFactory : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            throw new InvalidOperationException("测试节点类型不建立运行器。");
    }
}
