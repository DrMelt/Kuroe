using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Node.Input.Json;

/// <summary>输入节点的文件形状：声明只有提问文本，未知字段与形状由文件层报出。</summary>
internal sealed class InputTypeFile(IInputChannel channel) : INodeTypeFile
{
    private const string QuestionName = "Question";

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>输入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => InputNodeTypes.Shape;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } = [new(QuestionName, ParameterValueKind.Text)];

    /// <summary>读出参数段并建出类型实例。</summary>
    public INodeType Read(IParameterReader values, Action<string> fail) =>
        InputNodeTypes.Create(new InputParameters { Question = values.Text(QuestionName) }, channel);

    /// <summary>写回参数段：只写有值的字段。</summary>
    public ParameterValues Write(INodeType? type)
    {
        ParameterValues values = new();
        if (type is InputNodeType { Parameters.Question: { Length: > 0 } question })
        {
            values.Set(QuestionName, question);
        }

        return values;
    }
}