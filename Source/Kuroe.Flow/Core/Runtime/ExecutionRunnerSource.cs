using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using NeutralExecutionRunner = Kuroe.Flow.Execution.IExecutionRunner;
using NeutralExecutionId = Kuroe.Flow.Execution.ExecutionId;
using NeutralRunnerFactory = Kuroe.Flow.Execution.IExecutionRunnerFactory;
using OldExecutionRunner = Kuroe.Flow.Shared.RuntimeGraph.Execution.IExecutionRunner;
using OldRunnerFactory = Kuroe.Flow.Shared.RuntimeGraph.Execution.IExecutionRunnerFactory;
using GraphRunnerSource = Kuroe.Flow.Shared.RuntimeGraph.Execution.IExecutionRunnerSource;
using OldExecutionId = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId;

namespace Kuroe.Flow.Runtime;

/// <summary>按节点类型选择执行器与恢复器：类型自带中性实现时用类型实现，图侧旧实现经适配后使用，否则用默认执行器。</summary>
public sealed class ExecutionRunnerSource(OldRunnerFactory fallback) : GraphRunnerSource, IExecutionRestorerSource
{
    /// <summary>为一次执行建立中性运行器。</summary>
    public NeutralExecutionRunner Create(
        ExecutableNode node,
        IExecutionTarget target,
        OldExecutionId execution)
    {
        if (node.Type is NeutralRunnerFactory factory)
        {
            return factory.Create(target, new NeutralExecutionId(execution.Value));
        }

        if (node.Type.ModelPort is not null)
        {
            return Wrap(fallback.Create(node, execution), target);
        }

        throw new InvalidOperationException($"节点类型 {node.Type.Kind.Value} 既没有自带执行器，也没有模型端口。");
    }

    /// <summary>校验恢复数据并装配一次恢复。</summary>
    public ErrorOr<PreparedRestore> Prepare(
        ExecutableNode node,
        RestorePreparationContext context) =>
        RestorerOf(node) is { } restorer
            ? restorer.Prepare(context)
            : Error.Failure("Execution.Restore", $"节点类型 {node.Type.Kind.Value} 不支持恢复。");

    /// <summary>按恢复结果建立运行器。</summary>
    public OldExecutionRunner CreateRestored(
        ExecutableNode node,
        OldExecutionId execution,
        PreparedRestore restore) =>
        RestorerOf(node) is { } restorer
            ? restorer.CreateRestored(node, execution, restore)
            : throw new InvalidOperationException($"节点类型 {node.Type.Kind.Value} 不支持恢复。");

    /// <summary>节点类型或默认执行器提供的恢复能力。</summary>
    private INodeExecutionRestorer? RestorerOf(ExecutableNode node) =>
        node.Type as INodeExecutionRestorer
        ?? (node.Type.ModelPort is not null ? fallback as INodeExecutionRestorer : null);

    private static NeutralExecutionRunner Wrap(OldExecutionRunner runner, IExecutionTarget target) =>
        target is GraphExecutionTarget graph
            ? new LegacyExecutionRunnerAdapter(graph.Input, runner)
            : throw new InvalidOperationException("图侧旧运行器缺少图侧执行输入。");
}