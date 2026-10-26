using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.File;

/// <summary>文件读取节点类型：不启动执行，激活时按 BasePath 解析路径并读取文件内容，写入唯一的输出端口。</summary>
public sealed class FileReadNodeType(FileParameters? parameters) : INodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public FileParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => FileReadNodeTypes.Name;

    /// <summary>文件读取节点类型的静态形状。</summary>
    public NodeTypeShape Shape => FileReadNodeTypes.Shape;

    /// <summary>文件读取节点的类型规则：BasePath 给出路径解析基准，其余输入只作触发，输出只承载读取内容，不接受执行方式与校验。</summary>
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

        Reject(node.Model is not null, "文件读取节点不启动执行，不能声明模型选择。");
        Reject(spec.Validate is not null, "文件读取节点不启动执行，不能声明 Validate。");
        Reject(!node.Inputs.Any(port => port.Type.Semantics == PortSemantics.Signal),
            "文件读取节点必须声明至少一个 Signal 输入端口作为触发。");
        Reject(node.Inputs.Any(port => port.Name != FileNodePorts.BasePath && Ports.CarriesText(port.Type)),
            "文件读取节点只有 BasePath 可以是 Text 输入端口。");

        InputPort? basePath = node.Inputs.Find(FileNodePorts.BasePath);
        Reject(basePath is null, "文件读取节点必须声明 BasePath 输入端口作为路径解析基准。");
        Reject(basePath is { } declared && !Ports.CarriesText(declared.Type), "BasePath 输入端口只能是 Text。");

        Reject(node.Outputs.Count != 1, "文件读取节点必须声明一个输出端口，读取内容写入该端口。");
        Reject(spec.MaxExecutions is { } limit && limit < 1, "MaxExecutions 必须是正整数。");
    }

    /// <summary>文件读取节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) =>
        new FileReadRuntime((ExecutableNode)node, Parameters
            ?? throw new InvalidOperationException("文件读取节点缺少 Path 声明，无法建立运行期对象。"));
}