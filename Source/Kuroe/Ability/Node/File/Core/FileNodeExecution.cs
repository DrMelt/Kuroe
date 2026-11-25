using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.Ability.Runner.File;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Ability.Node.File;

/// <summary>文件节点的执行器能力：类型声明本身就是执行参数。</summary>
public static class FileNodeExecution
{
    /// <summary>按类型实例建立读取执行器绑定。</summary>
    public static IExecutionBinder? BindRead(
        INodeType type,
        IExecutionRunnerFactory runnerFactory) =>
        type is FileReadNodeType node
            ? Bind(node.Parameters, runnerFactory, "文件读取节点缺少 Path 声明，无法建立执行器。")
            : null;

    /// <summary>按类型实例建立写入执行器绑定。</summary>
    public static IExecutionBinder? BindWrite(
        INodeType type,
        IExecutionRunnerFactory runnerFactory) =>
        type is FileWriteNodeType node
            ? Bind(node.Parameters, runnerFactory, "文件写入节点缺少 Path 声明，无法建立执行器。")
            : null;

    private static ExecutionBinder Bind(
        FileExecutionParameters? parameters,
        IExecutionRunnerFactory runnerFactory,
        string reason) =>
        new(runnerFactory, _ => parameters is { } value
            ? (ErrorOr<IExecutionParameters>)value
            : Error.Validation(FlowErrorCodes.FlowComposition, reason));
}
