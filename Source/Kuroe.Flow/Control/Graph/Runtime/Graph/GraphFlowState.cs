using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.StaticGraph.Flow;

namespace Kuroe.Flow.Control.Graph;

/// <summary>图流程的运行状态：锁定的编译结果、开始端口值、运行期节点与执行上下文。</summary>
internal sealed class GraphFlowState : IGraphFlowState
{
    private readonly Dictionary<ExecutionId, GraphExecutionContext> _executions = [];
    private readonly IFlowExecutionGateway _executionsRuntime;
    private ITaskFlowContext? _task;
    private TaskRuntime? _runtime;

    public GraphFlowState(
        CompiledFlow flow,
        StartInputs inputs,
        IPortTexts startTexts,
        StartPortValues startPorts,
        GraphFlowDefinitionRecord? definition,
        IFlowExecutionGateway executions)
    {
        Flow = flow;
        Inputs = inputs;
        StartTexts = startTexts;
        StartPorts = startPorts;
        Definition = definition;
        _executionsRuntime = executions;
    }

    /// <summary>锁定的编译流程。</summary>
    public CompiledFlow Flow { get; }

    /// <summary>提交时的开始输入。</summary>
    public StartInputs Inputs { get; }

    /// <summary>任务启动时的开始端口文本。</summary>
    public IPortTexts StartTexts { get; }

    /// <summary>任务启动时确定的开始端口值。</summary>
    public StartPortValues StartPorts { get; }

    /// <summary>提交时锁定的流程定义记录。</summary>
    public GraphFlowDefinitionRecord? Definition { get; set; }

    /// <summary>图运行期的执行，按提交顺序从任务的中性执行列表投影。</summary>
    internal IReadOnlyList<GraphExecution> Executions => Project();

    IReadOnlyList<GraphExecution> IGraphFlowState.Executions => Project();

    /// <summary>图运行期状态，任务建立后创建。</summary>
    public TaskRuntime Runtime => _runtime ?? throw new InvalidOperationException("图流程状态还没有运行期。");

    /// <summary>图运行期是否已接到任务。</summary>
    public bool Attached => _runtime is not null;

    /// <summary>记下一个执行在图侧的上下文。要求持有任务 Gate。</summary>
    internal void Bind(ExecutionId id, GraphExecutionInput input, int controlVersion) =>
        _executions[id] = new GraphExecutionContext(input, controlVersion);

    /// <summary>按执行号取图侧上下文，不存在时抛出。</summary>
    internal GraphExecutionContext Context(ExecutionId id) =>
        _executions.TryGetValue(id, out GraphExecutionContext? context)
            ? context
            : throw new InvalidOperationException($"图流程没有执行 {id.Value} 的上下文。");

    private IReadOnlyList<GraphExecution> Project()
    {
        if (_task is not { } task)
        {
            throw new InvalidOperationException("图流程状态还没有接到任务。");
        }

        List<GraphExecution> executions = [];
        foreach (IExecution unit in task.Executions)
        {
            GraphExecutionContext context = Context(unit.Id);
            executions.Add(new GraphExecution(
                context.Input,
                unit,
                context.ControlVersion));
        }

        return executions;
    }

    /// <summary>存档恢复的节点状态。</summary>
    public IReadOnlyList<NodeRecord> RestoreNodes { get; init; } = [];

    /// <summary>存档恢复的端口发布历史。</summary>
    public IReadOnlyList<PortPublicationRecord> RestorePorts { get; init; } = [];

    /// <summary>存档恢复的待处理步骤。</summary>
    public FlowSeed Seed { get; init; } = FlowSeed.Empty;

    /// <summary>存档保存时刻。</summary>
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>把任务上下文接到图运行期。</summary>
    public void Attach(ITaskFlowContext task)
    {
        _task = task;
        _runtime = new TaskRuntime(task, this, _executionsRuntime);
    }
}

/// <summary>一次执行在图侧的上下文：图输入与创建时的任务控制代次。</summary>
internal sealed record GraphExecutionContext(GraphExecutionInput Input, int ControlVersion);