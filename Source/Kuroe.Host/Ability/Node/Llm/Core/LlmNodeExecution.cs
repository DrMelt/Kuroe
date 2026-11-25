using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.FlowDescription.Definition.Shared.Nodes;

namespace Kuroe.Host.Ability.Node.Llm;

/// <summary>Llm 节点的执行器能力：从类型实例上的声明建立参数。</summary>
public static class LlmNodeExecution
{
    /// <summary>按类型实例建立执行器绑定，类型不匹配时为空。</summary>
    public static IExecutionBinder? Bind(
        INodeType type,
        IExecutionRunnerFactory runnerFactory) =>
        type is LlmNodeType node
            ? new ExecutionBinder(
                runnerFactory,
                context => (node.Parameters ?? new LlmParameters()).ToExecutionParameters(
                    context.Model,
                    attempt: context.Attempt,
                    maxAttempts: context.MaxAttempts))
            : null;
}
