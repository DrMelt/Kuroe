using Kuroe.Executor.Shared.Parameters;
using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.Graph.Shared.StaticGraph.Validation;
using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Node.File;

/// <summary>文件读取节点的描述契约：声明只有路径，未知字段与形状由构造层报出。</summary>
internal sealed class FileReadNodeTypeDescription(
    string rootDirectory,
    IExecutionRunnerFactory runnerFactory) : INodeTypeDescription
{
    private readonly IExecutionRunnerFactory _runnerFactory = runnerFactory;
    /// <summary>形状所属的节点类型名。</summary>
    public NodeKind Kind => FileReadNodeTypes.Name;

    /// <summary>文件读取节点类型的静态形状。</summary>
    public NodeTypeShape Shape => FileReadNodeTypes.Shape;

    /// <summary>参数段声明的字段与取值形态。</summary>
    public IReadOnlyList<ParameterField> Fields { get; } =
    [
        new(FileParameterNames.Path, ParameterValueKind.Text),
        new(FileParameterNames.Variables, ParameterValueKind.Map),
        new(FileParameterNames.MaxChars, ParameterValueKind.Integer),
    ];

    /// <summary>读出参数段并建出类型实例。</summary>
    public INodeType Read(IParameterReader values, ValidationMessageHandler fail) =>
        FileReadNodeTypes.Create(
            FileTargetReader.Read(values, fail, "文件读取节点", rootDirectory, FileReadNodeTypes.DefaultMaxChars),
            _runnerFactory);
}