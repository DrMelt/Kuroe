using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Graph.Shared.StaticGraph.Validation;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Node.Input;

/// <summary>输入节点的描述契约：声明只有提问文本，未知字段与形状由构造层报出。</summary>
internal sealed class InputNodeTypeDescription(IExecutionRunnerFactory runnerFactory) : INodeTypeDescription
{
    private const string QuestionName = "Question";

    private readonly IExecutionRunnerFactory _runnerFactory = runnerFactory;

    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => InputNodeTypes.Name;

    /// <summary>输入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => InputNodeTypes.Shape;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } = [new(QuestionName, ParameterValueKind.Text)];

    /// <summary>读出参数段并建出类型实例。</summary>
    public INodeType Read(IParameterReader values, ValidationMessageHandler fail) =>
        InputNodeTypes.Create(
            new InputParameters { Question = values.Text(QuestionName) },
            _runnerFactory);
}