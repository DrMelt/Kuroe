using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Node.Approval;

/// <summary>审批节点的描述契约：声明只有提问文本，未知字段与形状由构造层报出。</summary>
internal sealed class ApprovalNodeTypeDescription(IInputChannel channel) : INodeTypeDescription
{
    private const string QuestionName = "Question";

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => ApprovalNodeTypes.Name;

    /// <summary>审批节点类型的静态形状。</summary>
    public NodeTypeShape Shape => ApprovalNodeTypes.Shape;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } = [new(QuestionName, ParameterValueKind.Text)];

    /// <summary>读出参数段并建出类型实例。</summary>
    public INodeType Read(IParameterReader values, Action<string> fail) =>
        ApprovalNodeTypes.Create(new ApprovalParameters { Question = values.Text(QuestionName) }, channel);
}