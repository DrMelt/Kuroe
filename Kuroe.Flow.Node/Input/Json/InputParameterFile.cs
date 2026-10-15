using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Input.Json;

/// <summary>输入节点参数段的文件形状：参数只有提问文本，未知字段与形状由文件层报出。</summary>
internal sealed class InputParameterFile : INodeParameterFile
{
    private const string QuestionName = "Question";

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } = [new(QuestionName, ParameterValueKind.Text)];

    /// <summary>读出参数段。</summary>
    public INodeParameters? Read(IParameterReader values, Action<string> fail) =>
        new InputParameters { Question = values.Text(QuestionName) };

    /// <summary>写回参数段：只写有值的字段。</summary>
    public ParameterValues Write(INodeParameters? parameters)
    {
        ParameterValues values = new();
        if (parameters is InputParameters input && input.Question is { Length: > 0 } question)
        {
            values.Set(QuestionName, question);
        }

        return values;
    }
}