using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Approval.Json;

/// <summary>审批节点参数段的文件形状：参数只有提问文本，未知字段与形状由文件层报出。</summary>
internal sealed class ApprovalParameterFile : INodeParameterFile
{
    private const string QuestionName = "Question";

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => ApprovalNodeTypes.Name;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } = [new(QuestionName, ParameterValueKind.Text)];

    /// <summary>读出参数段。</summary>
    public INodeParameters? Read(IParameterReader values, Action<string> fail) =>
        new ApprovalParameters { Question = values.Text(QuestionName) };

    /// <summary>写回参数段：只写有值的字段。</summary>
    public ParameterValues Write(INodeParameters? parameters)
    {
        ParameterValues values = new();
        if (parameters is ApprovalParameters approval && approval.Question is { Length: > 0 } question)
        {
            values.Set(QuestionName, question);
        }

        return values;
    }
}