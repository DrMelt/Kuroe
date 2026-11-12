using ErrorOr;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using NeutralExecution = Kuroe.Flow.Execution.Execution;
using NeutralExecutionContract = Kuroe.Flow.Execution.ExecutionContract;
using NeutralExecutionId = Kuroe.Flow.Execution.ExecutionId;
using NeutralExecutionOutput = Kuroe.Flow.Execution.ExecutionOutput;
using NeutralExecutionOutputKind = Kuroe.Flow.Execution.ExecutionOutputKind;
using ExecutionInputPort = Kuroe.Flow.Execution.ExecutionInputPort;
using ExecutionInputValue = Kuroe.Flow.Execution.ExecutionInputValue;
using ExecutionOutputPort = Kuroe.Flow.Execution.ExecutionOutputPort;
using ExecutionOutputValue = Kuroe.Flow.Execution.ExecutionOutputValue;
using ExecutionOwnerId = Kuroe.Flow.Execution.ExecutionOwnerId;
using ExecutionPortKind = Kuroe.Flow.Execution.ExecutionPortKind;
using ExecutionPortName = Kuroe.Flow.Execution.ExecutionPortName;
using ExecutionPortRole = Kuroe.Flow.Execution.ExecutionPortRole;
using ExecutionRequest = Kuroe.Flow.Execution.ExecutionRequest;
using ExecutionRestoreState = Kuroe.Flow.Execution.ExecutionRestoreState;
using ExecutionUsage = Kuroe.Flow.Execution.ExecutionUsage;
using IExecutionRestoreDataSource = Kuroe.Flow.Execution.IExecutionRestoreDataSource;
using IExecutionTarget = Kuroe.Flow.Execution.IExecutionTarget;
using IExecutionUsageSource = Kuroe.Flow.Execution.IExecutionUsageSource;
using ExecutionParameterContext = Kuroe.Flow.Execution.ExecutionParameterContext;
using TokenUsage = Kuroe.Flow.Execution.TokenUsage;

namespace Kuroe.Flow.Runtime;

/// <summary>一次执行。状态机由执行单元承担，这里装配图侧输入并转换执行单元的中性产出与契约。</summary>
public sealed class GraphExecution : IGraphExecution
{
    private readonly NeutralExecution _unit;

    /// <summary>底层中性执行状态机，供中性派发器使用。</summary>
    internal NeutralExecution Unit => _unit;

    /// <summary>快照可见的状态或过程变化。</summary>
    internal event Action? Changed
    {
        add => _unit.Changed += value;
        remove => _unit.Changed -= value;
    }

    internal GraphExecution(ExecutionId id, ExecutionInput input)
        : this(id, input, 0)
    {
    }

    internal GraphExecution(ExecutionId id, ExecutionInput input, int controlVersion)
    {
        Input = input;
        ControlVersion = controlVersion;
        Request = BuildRequest(input);
        Target = BuildTarget(input, Request);
        _unit = new NeutralExecution(new NeutralExecutionId(id.Value), Request);
    }

    internal GraphExecution(ExecutionInput input, NeutralExecution unit, int controlVersion)
    {
        Input = input;
        ControlVersion = controlVersion;
        Request = unit.Request;
        Target = BuildTarget(input, Request);
        _unit = unit;
    }

    private GraphExecution(ExecutionRecord record, ExecutionInput input)
    {
        Input = input;
        Request = BuildRequest(input);
        Target = BuildTarget(input, Request);
        _unit = NeutralExecution.Restore(
            new NeutralExecutionId(record.Id.Value),
            Request,
            record.CreatedAt,
            record.State,
            record.Progress,
            record.StartedAt,
            record.FinishedAt,
            record.Failures,
            FromRecord(record.Usage),
            record.RestoreData is { } data ? new ExecutionRestoreState(data.Format, data.Payload) : null,
            NeutralContract(record.Contract),
            record.ContinuedFrom is { } continued ? new NeutralExecutionId(continued.Value) : null);
    }

    /// <summary>从存档恢复一次已收口的执行。未收口的执行记为已中断。</summary>
    internal static GraphExecution Restore(ExecutionRecord record, ExecutionInput input) => new(record, input);

    /// <summary>执行标识。</summary>
    public ExecutionId Id => new(_unit.Id.Value);

    /// <summary>本次执行的输入。</summary>
    public ExecutionInput Input { get; }

    /// <summary>本次执行的中性请求。</summary>
    public ExecutionRequest Request { get; }

    /// <summary>本次执行的图侧目标。</summary>
    internal IExecutionTarget Target { get; }

    /// <summary>执行创建时刻。</summary>
    public DateTimeOffset CreatedAt => _unit.CreatedAt;

    /// <summary>创建时的任务控制代次，用于收口时判断控制动作是否已经发生。</summary>
    internal int ControlVersion { get; }

    /// <summary>当前执行状态。</summary>
    public ExecutionState State => (ExecutionState)_unit.State;

    /// <summary>还在排队或正在执行。</summary>
    public bool IsLive => _unit.IsLive;

    internal CancellationToken CancellationToken => _unit.CancellationToken;

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

    internal void MarkRunning() => _unit.MarkRunning();

    /// <summary>执行进入运行态后触发一次，任务侧据此记录一次执行尝试。</summary>
    internal event Action? Started
    {
        add => _unit.Started += value;
        remove => _unit.Started -= value;
    }

    /// <summary>记下本次执行的过程记录。</summary>
    internal void AttachJournal(IProcessJournal journal) => _unit.AttachJournal(journal);

    /// <summary>附加本次执行的用量来源，快照读取其当前累计。</summary>
    internal void AttachUsageReader(IExecutionUsageSource reader) => _unit.AttachUsageReader(reader);

    /// <summary>记下本次执行的最终用量并解除用量来源。</summary>
    internal void SettleUsage(ExecutionUsage usage) => _unit.SettleUsage(usage);

    /// <summary>本次执行保留的恢复数据，作为执行历史，成功与完成后仍在。</summary>
    internal ExecutionRestorePayload? RestoreData => ToRestorePayload(_unit.RestoreData);

    /// <summary>恢复数据变化后触发，存档捕获据此写盘。</summary>
    internal event Action? RestoreDataChanged
    {
        add => _unit.RestoreDataChanged += value;
        remove => _unit.RestoreDataChanged -= value;
    }

    /// <summary>本次执行从哪次执行恢复，独立执行时为空。</summary>
    internal ExecutionId? ContinuedFrom =>
        _unit.ContinuedFrom is { } continued ? new ExecutionId(continued.Value) : null;

    /// <summary>本次执行的本轮契约状态。</summary>
    internal ExecutionContract Contract => ToContract(_unit.Contract);
/// <summary>记下本次执行从哪次执行恢复，并继承来源执行的契约状态。</summary>
    internal void ContinueFrom(GraphExecution source) => _unit.ContinueFrom(source._unit);

    /// <summary>记下本次执行交回的端口值。</summary>
    internal void SubmitPorts(IReadOnlyDictionary<PortName, PortValue> ports) =>
        _unit.SubmitOutputs(ports.ToDictionary(
            pair => new ExecutionPortName(pair.Key.Value),
            pair => new ExecutionOutputValue(
                new ExecutionPortName(pair.Key.Value),
                pair.Value.Text,
                pair.Value.Fires)));

    /// <summary>记下本次执行上报的无法完成原因。</summary>
    internal void ReportCannotComplete(string reason) => _unit.ReportCannotComplete(reason);

    /// <summary>附加运行器的恢复数据读取面。</summary>
    internal void AttachRestoreDataReader(IExecutionRestoreDataSource reader) =>
        _unit.AttachRestoreDataReader(reader);

    /// <summary>记下运行器的最终恢复数据并解除读取面。</summary>
    internal void SettleRestoreData(IExecutionRestoreDataSource? reader) =>
        _unit.SettleRestoreData(reader);

    /// <summary>本次执行的产出。</summary>
    internal ExecutionOutput? Output => _unit.Output is { } output ? ToOutput(output) : null;

    internal void MarkSucceeded(NeutralExecutionOutput output)
    {
        NeutralExecutionOutput settled = output.Kind == NeutralExecutionOutputKind.Ports && output.Values.Count == 0
            ? NeutralExecutionOutput.FromPorts(_unit.Contract.Outputs?.Values.ToArray() ?? [])
            : output;
        _unit.MarkSucceeded(settled);
    }

    internal void MarkFailed(IEnumerable<Error> errors) => _unit.MarkFailed(errors);

    /// <summary>请求中断本次执行，进行中的一轮请求随之结束。要求持有任务 Gate。</summary>
    internal void Interrupt(string reason = "已中断。") => _unit.Interrupt(reason);

    /// <summary>按中断请求收口，直接记为已中断，用于未进入执行的排队项与退出时的丢弃项。</summary>
    internal void MarkInterrupted(string? reason = null) => _unit.MarkInterrupted(reason);

    /// <summary>本次请求正常结束但没交回本执行节点要求的东西，从已完成降级为失败。</summary>
    internal void MarkUncollected(string reason) => _unit.MarkUncollected(reason);

    /// <summary>当前状态的只读快照。</summary>
    public GraphExecutionSnapshot Snapshot()
    {
        Kuroe.Flow.Execution.ExecutionSnapshot unit = _unit.Snapshot();

        return new GraphExecutionSnapshot(
            Id,
            Input,
            (ExecutionState)unit.State,
            unit.Progress,
            unit.CreatedAt,
            unit.StartedAt,
            unit.FinishedAt,
            unit.Failures,
            unit.Journal,
            unit.Usage,
            unit.RestoreData is not null,
            unit.ContinuedFrom is { } continued ? new ExecutionId(continued.Value) : null);
    }
/// <summary>捕获本次执行的状态与恢复数据。</summary>
    internal ExecutionRecord CaptureRecord()
    {
        GraphExecutionSnapshot snapshot = Snapshot();
        ExecutionContract contract = Contract;

        return new ExecutionRecord(
            new NeutralExecutionId(snapshot.Id.Value),
            GraphRecordTargets.Format(snapshot.Input.Node),
            snapshot.Input.ExecutionCount,
            (Kuroe.Flow.Execution.ExecutionState)snapshot.State,
            snapshot.Progress,
            snapshot.CreatedAt,
            snapshot.StartedAt,
            snapshot.FinishedAt,
            snapshot.Failures,
            new ExecutionUsageRecord(
                snapshot.Usage.RequestCount,
                snapshot.Usage.RequestsWithoutUsage,
                new TokenUsageRecord(
                    snapshot.Usage.Tokens.Input,
                    snapshot.Usage.Tokens.Output,
                    snapshot.Usage.Tokens.Total,
                    snapshot.Usage.Tokens.CachedInput,
                    snapshot.Usage.Tokens.Reasoning),
                snapshot.Usage.ContextTokens),
            [
                .. snapshot.Input.Inputs.Select(value => new ExecutionInputRecord(
                    new ExecutionPortName(value.Port.Value),
                    GraphRecordSources.Write(value.Value.Origin),
                    value.Text)),
            ],
            snapshot.Input.Texts.ToDictionary(
                pair => new ExecutionPortName(pair.Key.Value),
                pair => pair.Value),
            snapshot.HasRestoreData && RestoreData is { } restore
                ? new ExecutionRestoreRecord(restore.Format, restore.Payload)
                : null,
            snapshot.ContinuedFrom is { } continued
                ? new NeutralExecutionId(continued.Value)
                : null,
            contract is { Ports: null, CannotCompleteReason: null }
                ? null
                : new ExecutionContractRecord(
                    contract.Ports?.ToDictionary(
                        pair => new ExecutionPortName(pair.Key.Value),
                        pair => new ExecutionOutputValue(
                            new ExecutionPortName(pair.Key.Value),
                            pair.Value.Text,
                            pair.Value.Fires)),
                    contract.CannotCompleteReason));
    }
internal static ExecutionRequest BuildRequest(ExecutionInput input) =>
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
                    SourceOf(value.Value.Origin))),
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
                        required,
                        port.Mode == PortMode.Constant,
                        port.Description);
                }),
            ]);

    internal static IExecutionTarget BuildTarget(ExecutionInput input, ExecutionRequest request) =>
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

    private static NeutralExecutionContract NeutralContract(ExecutionContractRecord? contract) =>
        contract is null
            ? NeutralExecutionContract.Empty
            : new NeutralExecutionContract(
                contract.Outputs?.ToDictionary(
                    pair => new ExecutionPortName(pair.Key.Value),
                    pair => new ExecutionOutputValue(
                        new ExecutionPortName(pair.Key.Value),
                        pair.Value.Text,
                        pair.Value.Fires)),
                contract.CannotCompleteReason);

    private static ExecutionContract ToContract(NeutralExecutionContract contract) =>
        new(
            contract.Outputs?.ToDictionary(
                pair => new PortName(pair.Key.Value),
                pair => new PortValue(pair.Value.Text, pair.Value.Fires)),
            contract.CannotCompleteReason);

    /// <summary>把图侧契约状态转为中性形状，供恢复计划装配使用。</summary>
    internal static NeutralExecutionContract ToNeutralContract(ExecutionContract contract) =>
        new(
            contract.Ports?.ToDictionary(
                pair => new ExecutionPortName(pair.Key.Value),
                pair => new ExecutionOutputValue(
                    new ExecutionPortName(pair.Key.Value),
                    pair.Value.Text,
                    pair.Value.Fires)),
            contract.CannotCompleteReason);

    private static ExecutionRestorePayload? ToRestorePayload(ExecutionRestoreState? state) =>
        state is null ? null : new ExecutionRestorePayload(state.Format, state.Payload);

    private static ExecutionOutput ToOutput(NeutralExecutionOutput output) =>
        output.Kind == NeutralExecutionOutputKind.Content
            ? ExecutionOutput.FromContent(output.Content)
            : ExecutionOutput.Ports;
}