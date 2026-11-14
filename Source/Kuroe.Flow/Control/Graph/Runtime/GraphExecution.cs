using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Execution.Records.Shared;
using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime;

/// <summary>一次执行。状态机由执行单元承担，这里装配图侧输入并转换执行单元的中性产出与契约。</summary>
public sealed class GraphExecution : IGraphExecution
{
    private readonly IExecution _unit;

    /// <summary>底层中性执行，供中性派发器使用。</summary>
    internal IExecution Unit => _unit;

    /// <summary>快照可见的状态或过程变化。</summary>
    internal event Action? Changed
    {
        add => _unit.Changed += value;
        remove => _unit.Changed -= value;
    }

    internal GraphExecution(
        GraphExecutionInput input,
        IExecution unit,
        int controlVersion)
    {
        Input = input;
        ControlVersion = controlVersion;
        Request = unit.Request;
        Target = BuildTarget(input, Request);
        _unit = unit;
    }

    /// <summary>从存档恢复一次已收口的执行。未收口的执行记为已中断。</summary>
    internal static GraphExecution Restore(
        ExecutionRecord record,
        GraphExecutionInput input,
        IFlowExecutionGateway executions)
    {
        ExecutionRequest request = BuildRequest(input);
        IExecution unit = executions.Restore(
            new ExecutionRestorePlan(
                record.Id,
                request,
                record.CreatedAt,
                record.State,
                record.Progress,
                record.StartedAt,
                record.FinishedAt,
                record.Failures,
                FromRecord(record.Usage),
                record.RestoreData is { } data ? new ExecutionRestoreState(data.Format, data.Payload) : null,
                NeutralContract(record.Contract),
                record.ContinuedFrom));

        return new GraphExecution(input, unit, 0);
    }

    /// <summary>执行标识。</summary>
    public ExecutionId Id => _unit.Id;

    /// <summary>本次执行的输入。</summary>
    public GraphExecutionInput Input { get; }

    /// <summary>本次执行的中性请求。</summary>
    public ExecutionRequest Request { get; }

    /// <summary>本次执行的图侧目标。</summary>
    internal IExecutionTarget Target { get; }

    /// <summary>执行创建时刻。</summary>
    public DateTimeOffset CreatedAt => _unit.CreatedAt;

    /// <summary>创建时的任务控制代次，用于收口时判断控制动作是否已经发生。</summary>
    internal int ControlVersion { get; }

    /// <summary>当前执行状态。</summary>
    public ExecutionState State => _unit.State;

    /// <summary>还在排队或正在执行。</summary>
    public bool IsLive => _unit.IsLive;

    /// <summary>模型输出的一段流式内容，按到达顺序给出。</summary>
    public event ProcessContentHandler? ContentEmitted
    {
        add => _unit.ContentEmitted += value;
        remove => _unit.ContentEmitted -= value;
    }

    /// <summary>一条需要即时呈现的过程记录。</summary>
    public event JournalEntryHandler? EntryEmitted
    {
        add => _unit.EntryEmitted += value;
        remove => _unit.EntryEmitted -= value;
    }

    /// <summary>执行进入运行态后触发一次，任务侧据此记录一次执行尝试。</summary>
    internal event Action? Started
    {
        add => _unit.Started += value;
        remove => _unit.Started -= value;
    }

    /// <summary>本次执行保留的恢复数据，作为执行历史，成功与完成后仍在。</summary>
    internal ExecutionRestoreState? RestoreData => _unit.RestoreData;

    /// <summary>恢复数据变化后触发，存档捕获据此写盘。</summary>
    internal event Action? RestoreDataChanged
    {
        add => _unit.RestoreDataChanged += value;
        remove => _unit.RestoreDataChanged -= value;
    }

    /// <summary>本次执行从哪次执行恢复，独立执行时为空。</summary>
    internal ExecutionId? ContinuedFrom => _unit.ContinuedFrom;

    /// <summary>本次执行的本轮契约状态。</summary>
    internal GraphExecutionContract Contract => ToContract(_unit.Contract);

    /// <summary>本次执行的产出。</summary>
    internal ExecutionOutput? Output => _unit.Output;

    /// <summary>当前状态的只读快照。</summary>
    public GraphExecutionSnapshot Snapshot()
    {
        ExecutionSnapshot unit = _unit.Snapshot();

        return new GraphExecutionSnapshot(
            _unit.Id,
            Input,
            unit.State,
            unit.Progress,
            unit.CreatedAt,
            unit.StartedAt,
            unit.FinishedAt,
            unit.Failures,
            unit.Journal,
            unit.Usage,
            unit.RestoreData is not null,
            unit.ContinuedFrom);
    }

    internal static ExecutionRequest BuildRequest(GraphExecutionInput input) =>
        new(
            new ExecutionOwnerId(input.Task.Value),
            GraphRecordTargets.Format(input.Node),
            input.Type.Kind.Value,
            input.ExecutionCount,
            [
                .. input.InputPorts.Select(port => new ExecutionInputPort(
                    new ExecutionPortName(port.Name.Value),
                    KindOf(port.Type),
                    RoleOf(port.Role),
                    port.Role == InputRole.Content,
                    port.Default)),
            ],
            [
                .. input.Inputs.Select(value => new ExecutionInputValue(
                    new ExecutionPortName(value.Port.Value),
                    value.Text,
                    SourceOf(value.Value.Origin),
                    GraphRecordSources.Write(value.Value.Origin))),
            ],
            input.Texts.ToDictionary(
                pair => new ExecutionPortName(pair.Key.Value),
                pair => pair.Value),
            [
                .. input.OutputPorts.Select(port =>
                {
                    bool required = input.RequiredPorts.Any(required => required.Name == port.Name);

                    return new ExecutionOutputPort(
                        new ExecutionPortName(port.Name.Value),
                        KindOf(port.Type),
                        required,
                        port.Trigger,
                        port.Description);
                }),
            ],
            input.MaxAttempts);

    internal static IExecutionTarget BuildTarget(GraphExecutionInput input, ExecutionRequest request) =>
        new GraphExecutionTarget(input, new ExecutionParameterContext(
            request.Owner,
            request.Target,
            request.Kind,
            request.Attempt,
            request.InputPorts,
            request.Inputs,
            request.Texts,
            request.OutputPorts,
            input.Declaration?.Model));

    private static ExecutionPortRole RoleOf(InputRole role) => role switch
    {
        InputRole.Configuration => ExecutionPortRole.Configuration,
        InputRole.Control => ExecutionPortRole.Control,
        _ => ExecutionPortRole.Content,
    };

    private static string? SourceOf(ContextSource source) => source switch
    {
        ExecutionSource execution => $"节点「{execution.Node}」的端口「{execution.Port}」产出",
        ContainerPortSource container => $"节点「{container.Container}」的端口「{container.Port}」产出",
        InputSource input => $"节点「{input.Node}」的输入",
        StartSource start => $"流程起点端口「{start.Port}」",
        DefaultSource value => $"节点「{value.Node}」的端口「{value.Port}」默认值",
        _ => null,
    };

    private static ExecutionPortKind KindOf(PortType type)
    {
        if (type == PortTypes.Context)
        {
            return ExecutionPortKind.Context;
        }

        return type == PortTypes.Signal ? ExecutionPortKind.Signal : ExecutionPortKind.Text;
    }

    private static ExecutionUsage FromRecord(ExecutionUsageRecord usage) =>
        new(
            usage.RequestCount,
            usage.RequestsWithoutUsage,
            new TokenUsage(
                usage.Tokens.Input,
                usage.Tokens.Output,
                usage.Tokens.Total,
                usage.Tokens.CachedInput,
                usage.Tokens.Reasoning))
        {
            ContextTokens = usage.ContextTokens,
        };

    private static ExecutionContract NeutralContract(ExecutionContractRecord? contract) =>
        contract is null
            ? ExecutionContract.Empty
            : new ExecutionContract(
                contract.Outputs?.ToDictionary(
                    pair => new ExecutionPortName(pair.Key.Value),
                    pair => new ExecutionOutputValue(
                        new ExecutionPortName(pair.Key.Value),
                        pair.Value.Text,
                        pair.Value.Fires)),
                contract.CannotCompleteReason);

    private static GraphExecutionContract ToContract(ExecutionContract contract) =>
        new(
            contract.Outputs?.ToDictionary(
                pair => new PortName(pair.Key.Value),
                pair => new PortValue(pair.Value.Text, pair.Value.Fires)),
            contract.CannotCompleteReason);

    /// <summary>把图侧契约状态转为中性形状，供恢复计划装配使用。</summary>
    internal static ExecutionContract ToNeutralContract(GraphExecutionContract contract) =>
        new(
            contract.Ports?.ToDictionary(
                pair => new ExecutionPortName(pair.Key.Value),
                pair => new ExecutionOutputValue(
                    new ExecutionPortName(pair.Key.Value),
                    pair.Value.Text,
                    pair.Value.Fires)),
            contract.CannotCompleteReason);
}