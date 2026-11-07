using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime;

/// <summary>按节点类型选择执行器与恢复器：类型自带实现时用类型实现，否则用默认执行器。</summary>
public sealed class ExecutionRunnerSource(IExecutionRunnerFactory fallback) : IExecutionRunnerSource, IExecutionRestorerSource
{
    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner Create(ExecutableNode node, ExecutionId execution)
    {
        if (node.Type is INodeExecutionRunnerFactory factory)
        {
            return factory.Create(node, execution);
        }

        if (node.Type.ModelPort is not null)
        {
            return fallback.Create(node, execution);
        }

        throw new InvalidOperationException($"节点类型 {node.Type.Kind.Value} 既没有自带执行器，也没有模型端口。");
    }

    /// <summary>校验恢复状态并装配一次恢复。</summary>
    public ErrorOr<PreparedRestore> Prepare(
        ExecutableNode node,
        RestorePreparationContext context) =>
        RestorerOf(node) is { } restorer
            ? restorer.Prepare(context)
            : Error.Failure("Execution.Restore", $"节点类型 {node.Type.Kind.Value} 不支持恢复。");

    /// <summary>按恢复结果建立运行器。</summary>
    public IExecutionRunner CreateRestored(
        ExecutableNode node,
        ExecutionId execution,
        PreparedRestore restore) =>
        RestorerOf(node) is { } restorer
            ? restorer.CreateRestored(node, execution, restore)
            : throw new InvalidOperationException($"节点类型 {node.Type.Kind.Value} 不支持恢复。");

    /// <summary>节点类型或默认执行器提供的恢复能力。</summary>
    private INodeExecutionRestorer? RestorerOf(ExecutableNode node) =>
        node.Type as INodeExecutionRestorer
        ?? (node.Type.ModelPort is not null ? fallback as INodeExecutionRestorer : null);
}