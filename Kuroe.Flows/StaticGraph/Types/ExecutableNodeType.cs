using ErrorOr;
using Kuroe.Flows.RuntimeNodes;
using Kuroe.Flows.Shared;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flows.StaticGraph.Types;

/// <summary>执行节点类型：由模型执行并交回端口产出，引擎自带的机制型类型。</summary>
internal sealed class ExecutableNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeTypeName Type => NodeTypes.Executable;

    /// <summary>派发 run，输入输出端口取值域由模型产出的形态决定。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DispatchesRuns = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
        OutputTypes = [PortType.Text, PortType.WorkItems, PortType.Context],
    };

    /// <summary>执行节点的类型规则：执行上限、输出校验与按条目展开的组合约束。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        ExecutableSpec execution = node.Execution!;
        if (execution.MaxRuns is { } limit && limit < 1)
        {
            fail(scope.Node(node.Name.Value, "MaxRuns 必须是正整数。"));
        }

        if (execution.Validate is not null)
        {
            if (!node.Outputs.Any(port => port.Type == PortType.Text))
            {
                fail(scope.Node(node.Name.Value, "声明 Validate 的节点必须声明 Text 输出端口，校验对象是端口产出。"));
            }

            if (execution.Mode == NodeMode.PerItem)
            {
                fail(scope.Node(node.Name.Value, "声明 Validate 的执行节点不能按条目展开，必须是整节点执行。"));
            }
        }

        // Signal 输入只作驱动、不承载实例内容，按条目展开时仍成立；Context 输入没有实例映射
        if (execution.Mode == NodeMode.PerItem && node.Inputs.Any(port => port.Type == PortType.Context))
        {
            fail(scope.Node(node.Name.Value, "声明 Context 输入端口的执行节点不能按条目展开，必须是整节点执行。"));
        }
    }

    /// <summary>执行节点的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) => new RuntimeExecutable((ExecutableNode)node);
}
