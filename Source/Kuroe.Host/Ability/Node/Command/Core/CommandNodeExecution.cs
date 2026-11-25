using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Host.Ability.Node.Command;

/// <summary>命令节点的执行器能力：类型声明本身就是执行参数。</summary>
public static class CommandNodeExecution
{
    /// <summary>按类型实例建立执行器绑定，类型不匹配时为空。</summary>
    public static IExecutionBinder? Bind(
        INodeType type,
        IExecutionRunnerFactory runnerFactory) =>
        type is CommandNodeType node
            ? new ExecutionBinder(
                runnerFactory,
                _ => node.Parameters is { } value
                    ? (ErrorOr<IExecutionParameters>)value
                    : Error.Validation(
                    FlowErrorCodes.FlowComposition,
                    "命令执行节点缺少 Program 声明，无法建立执行器。"))
            : null;
}
