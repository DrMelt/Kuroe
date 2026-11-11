using Kuroe.Flow.Execution;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime;

/// <summary>图侧执行目标：执行标识取节点路径，种类取节点类型，参数由节点类型给出。</summary>
internal sealed class GraphExecutionTarget : IExecutionTarget
{
    internal GraphExecutionTarget(ExecutionInput input, ExecutionParameterContext context)
    {
        Input = input;
        Id = GraphRecordTargets.Format(input.Node);
        Kind = input.Type.Kind.Value;
        Parameters = input.Type is IExecutionParameterProvider provider
            ? provider.Create(context)
            : EmptyExecutionParameters.Instance;
    }

    /// <summary>执行目标标识，取节点路径。</summary>
    public ExecutionTargetId Id { get; }

    /// <summary>执行种类，取节点类型名。</summary>
    public string Kind { get; }

    /// <summary>节点类型给出的参数。</summary>
    public IExecutionParameters Parameters { get; }

    /// <summary>本次执行的图侧输入，供图侧旧运行器适配使用。</summary>
    internal ExecutionInput Input { get; }
}