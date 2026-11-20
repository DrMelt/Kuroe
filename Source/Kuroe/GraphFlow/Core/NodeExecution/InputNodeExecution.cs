using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Node.Input;
using Kuroe.Runner.Input;

namespace Kuroe.GraphFlow.NodeExecution;

/// <summary>输入节点的执行器能力与图能力注册。</summary>
public static class InputNodeExecution
{
    /// <summary>按类型实例建立执行器绑定，类型不匹配时为空。</summary>
    public static IExecutionBinder? Bind(
        INodeType type,
        IExecutionRunnerFactory runnerFactory) =>
        type is InputNodeType node
            ? new ExecutionBinder(
                runnerFactory,
                context => new InputExecutionParameters(
                    node.Parameters?.Question,
                    ExecutionStepPaths.ToFlowStepPath(context.Step)))
            : null;

    /// <summary>把输入节点的执行器与输出投影登记进图能力映射。</summary>
    public static void RegisterGraph(
        GraphNodeCapabilities capabilities,
        IExecutionRunnerFactory runnerFactory) =>
        capabilities.Register(
            InputNodeTypes.Name,
            type => Bind(type, runnerFactory),
            type => type is InputNodeType ? InputNodeOutputProjection.Instance : null);
}
