using Kuroe.Execution.Shared;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Control.Graph.Runtime;

/// <summary>图侧执行目标：执行标识取节点路径，执行器工厂类型由节点类型给出，参数由节点类型给出。</summary>
internal sealed class GraphExecutionTarget : IExecutionTarget
{
    internal GraphExecutionTarget(GraphExecutionInput input, ExecutionParameterContext context)
    {
        Input = input;
        Id = GraphRecordTargets.Format(input.Node);
        RunnerFactoryType = input.Type is IExecutionRunnerFactoryProvider runnerProvider
            ? runnerProvider.RunnerFactoryType
            : throw new InvalidOperationException($"节点类型 {input.Type.Kind.Value} 没有提供执行器。");
        Parameters = input.Type is IExecutionParameterProvider parameterProvider
            ? parameterProvider.Create(context)
            : EmptyExecutionParameters.Instance;
    }

    /// <summary>执行目标标识，取节点路径。</summary>
    public ExecutionTargetId Id { get; }

    /// <summary>执行器工厂类型，取节点类型。</summary>
    public Type RunnerFactoryType { get; }

    /// <summary>节点类型给出的参数。</summary>
    public IExecutionParameters Parameters { get; }

    /// <summary>本次执行的图侧输入，供图侧旧运行器适配使用。</summary>
    internal GraphExecutionInput Input { get; }
}