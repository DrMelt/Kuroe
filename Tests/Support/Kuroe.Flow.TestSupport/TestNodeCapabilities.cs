using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的图节点能力：测试执行节点只提供空参数与不建立运行器的工厂。</summary>
public static class TestNodeCapabilities
{
    /// <summary>建出测试能力映射。</summary>
    public static GraphNodeCapabilities Build()
    {
        GraphNodeCapabilities capabilities = new();
        capabilities.Register(
            TestNodeTypes.ExecutableKind,
            _ => new ExecutionBinder(
                new UnusedRunnerFactory(),
                _ => EmptyExecutionParameters.Instance));

        return capabilities;
    }

    private sealed class UnusedRunnerFactory : IExecutionRunnerFactory
    {
        public IExecutionRunner Create(
            IExecutionParameters parameters,
            IExecutionRequestEntry requests) =>
            throw new InvalidOperationException("测试节点类型不建立运行器。");
    }
}
