using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Process;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Command;

/// <summary>命令节点类型：不派发模型执行，激活时按 BasePath 解析执行目录并执行一次命令，把合并输出写入唯一的输出端口。</summary>
public sealed class CommandNodeType(CommandParameters? parameters, IProcessRunner process) : INodeType
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public CommandParameters? Parameters { get; } = parameters;

    /// <summary>进程执行边界。</summary>
    public IProcessRunner Process { get; } = process;

    /// <summary>类型名。</summary>
    public NodeKind Kind => CommandNodeTypes.Name;

    /// <summary>命令节点类型的静态形状。</summary>
    public NodeTypeShape Shape => CommandNodeTypes.Shape;

    /// <summary>命令节点的类型规则：BasePath 给出路径解析基准，其余输入只作触发，输出只承载命令输出，不接受模型选择与校验。</summary>
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

        Reject(node.Model is not null, "命令执行节点不启动模型执行，不能声明模型选择。");
        Reject(spec.Validate is not null, "命令执行节点不启动模型执行，不能声明 Validate。");
        Reject(!node.Inputs.Any(port => port.Type.Semantics == PortSemantics.Signal),
            "命令执行节点必须声明至少一个 Signal 输入端口作为触发。");
        Reject(node.Inputs.Any(port => port.Name != CommandNodePorts.BasePath && Ports.CarriesText(port.Type)),
            "命令执行节点只有 BasePath 可以是 Text 输入端口。");

        InputPort? basePath = node.Inputs.Find(CommandNodePorts.BasePath);
        Reject(basePath is null, "命令执行节点必须声明 BasePath 输入端口作为路径解析基准。");
        Reject(basePath is { } declared && !Ports.CarriesText(declared.Type), "BasePath 输入端口只能是 Text。");

        Reject(node.Outputs.Count != 1, "命令执行节点必须声明一个输出端口，命令输出写入该端口。");
        Reject(spec.MaxExecutions is { } limit && limit < 1, "MaxExecutions 必须是正整数。");
    }

    /// <summary>命令节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) =>
        new CommandRuntime((ExecutableNode)node, Parameters
            ?? throw new InvalidOperationException("命令执行节点缺少 Program 声明，无法建立运行期对象。"), Process);
}