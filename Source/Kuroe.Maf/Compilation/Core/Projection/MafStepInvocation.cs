using ErrorOr;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Flow.Graph.Shared.StaticGraph.Graph;
using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Compilation.Projection;

/// <summary>执行节点的 MAF 步骤装配：节点类型给出运行器工厂与执行参数。</summary>
internal sealed class MafStepInvocation(ExecutableNode executable) : IMafStepInvocationFactory
{
    /// <inheritdoc/>
    public ErrorOr<ExecutionInvocation> Create(
        MafStepId step,
        string model,
        ExecutionParameterContext context)
    {
        if (executable.Type is not IExecutionRunnerFactoryProvider runners)
        {
            return Error.Validation(FlowErrorCodes.FlowComposition,
                $"节点类型 {executable.Type.Kind.Value} 没有执行运行器工厂。");
        }

        IExecutionParameters parameters = executable.Type is IExecutionParameterProvider provider
            ? provider.Create(context with { Model = model })
            : EmptyExecutionParameters.Instance;

        return new ExecutionInvocation(step.ToExecutionStepId(), runners.RunnerFactory, parameters);
    }
}