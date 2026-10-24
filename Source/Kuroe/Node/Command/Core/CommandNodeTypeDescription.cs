using Kuroe.Flow.Shared.Descriptions;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Process;

namespace Kuroe.Node.Command;

/// <summary>命令节点的描述契约：声明程序、参数、执行目录、超时与输出上限，未知字段与形状由构造层报出。</summary>
internal sealed class CommandNodeTypeDescription(string workingDirectory, IProcessRunner process) : INodeTypeDescription
{
    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => CommandNodeTypes.Name;

    /// <summary>命令节点类型的静态形状。</summary>
    public NodeTypeShape Shape => CommandNodeTypes.Shape;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } =
    [
        new(CommandParameterNames.Program, ParameterValueKind.Text),
        new(CommandParameterNames.Arguments, ParameterValueKind.Texts),
        new(CommandParameterNames.Directory, ParameterValueKind.Text),
        new(CommandParameterNames.TimeoutSeconds, ParameterValueKind.Integer),
        new(CommandParameterNames.MaxChars, ParameterValueKind.Integer),
    ];

    /// <summary>读出参数段并建出类型实例。</summary>
    public INodeType Read(IParameterReader values, Action<string> fail) =>
        CommandNodeTypes.Create(CommandParameterReader.Read(values, fail, workingDirectory), process);
}