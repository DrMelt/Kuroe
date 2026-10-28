using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件写入节点类型：不启动执行，激活时按路径变量解析路径，把唯一的内容 Text 输入写入文件。</summary>
public sealed class FileWriteNodeType(FileParameters? parameters) : INodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public FileParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => FileWriteNodeTypes.Name;

    /// <summary>文件写入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => FileWriteNodeTypes.Shape;

    /// <summary>文件写入节点的类型规则：一个内容 Text 输入加若干路径变量 Text 输入，输出可选，不接受执行方式与校验。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec spec = node.Spec!;

        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Label.Value, message));
            }
        }

        Reject(spec.Validate is not null, "文件写入节点不启动执行，不能声明 Validate。");

        IReadOnlySet<string> variablePorts = FilePathVariables.Validate(node, Parameters, message =>
            fail(scope.Node(node.Label.Value, message)));
        Reject(node.Inputs.Count(port => Ports.CarriesText(port.Type) && !variablePorts.Contains(port.Name.Value)) != 1,
            "文件写入节点必须声明一个 Text 输入端口，写入内容取自它。");

        Reject(node.Outputs.Count > 1, "文件写入节点最多声明一个输出端口。");
        Reject(spec.MaxExecutions is { } limit && limit < 1, "MaxExecutions 必须是正整数。");
    }

    /// <summary>文件写入节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) =>
        new FileWriteRuntime((ExecutableNode)node, Parameters
            ?? throw new InvalidOperationException("文件写入节点缺少 Path 声明，无法建立运行期对象。"));
}