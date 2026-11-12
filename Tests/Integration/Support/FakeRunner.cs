using ErrorOr;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.TestSupport;
using Kuroe.Tool;
using NeutralExecutionId = Kuroe.Flow.Execution.ExecutionId;
using NeutralExecutionOutput = Kuroe.Flow.Execution.ExecutionOutput;
using NeutralExecutionOutputPort = Kuroe.Flow.Execution.ExecutionOutputPort;
using NeutralExecutionRequest = Kuroe.Flow.Execution.ExecutionRequest;
using NeutralExecutionRestoreContext = Kuroe.Flow.Execution.ExecutionRestoreContext;
using NeutralExecutionRestoreData = Kuroe.Flow.Execution.IExecutionRestoreData;
using NeutralExecutionRestoreDataSource = Kuroe.Flow.Execution.IExecutionRestoreDataSource;
using NeutralExecutionRestoreState = Kuroe.Flow.Execution.ExecutionRestoreState;
using NeutralExecutionRunner = Kuroe.Flow.Execution.IExecutionRunner;
using NeutralExecutionRunnerFactory = Kuroe.Flow.Execution.IExecutionRunnerFactory;
using NeutralExecutionRestorer = Kuroe.Flow.Execution.IExecutionRestorer;
using NeutralExecutionTarget = Kuroe.Flow.Execution.IExecutionTarget;
using NeutralExecutionUsage = Kuroe.Flow.Execution.ExecutionUsage;
using NeutralExecutionUsageSource = Kuroe.Flow.Execution.IExecutionUsageSource;
using NeutralPortKind = Kuroe.Flow.Execution.ExecutionPortKind;

namespace Kuroe.Integration.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，按声明收口，并提供恢复能力。</summary>
public sealed class FakeRunner : NeutralExecutionRunnerFactory, NeutralExecutionRestorer
{
    /// <inheritdoc/>
    string NeutralExecutionRunnerFactory.Kind => "*";

    /// <inheritdoc/>
    string NeutralExecutionRestorer.Kind => "*";
    private readonly Lock _gate = new();
    private readonly List<IExecution> _started = [];

    /// <summary>执行闸门：替身执行器在此登记进入并保持，测试等待进入并放行。</summary>
    public IExecutionGate Gate { get; } = new ExecutionGate();

    /// <summary>执行索引，用于按执行号取执行对象供测试断言。</summary>
    public ExecutionIndex? Executions { get; set; }

    /// <summary>按执行创建契约工具的工厂。</summary>
    public Func<ExecutionId, IReadOnlyList<ITool>>? ContractToolFactory { get; set; }

    /// <summary>声明需交回输出端口的执行节点交回的命名段。空时按执行声明的端口逐个生成：Text 端口给同名校验文本，Signal 端口只给端口名。</summary>
    public string PortValuesJson { get; set; } = string.Empty;

    /// <summary>按执行生成端口值 JSON，设置后优先于 <see cref="PortValuesJson"/> 与默认逐端口名生成；返回 null 时回退。用于按执行轮次区分端口内容的装配。</summary>
    public Func<IExecution, string?>? PortValuesJsonFor { get; set; }

    /// <summary>输出端口节点是否交回命名段，关掉用于验证未收口。</summary>
    public bool SubmitsPorts { get; set; } = true;

    /// <summary>是否上报本轮无法完成，默认不上报，即按本轮交回的内容收口。</summary>
    public bool CannotComplete { get; set; }

    /// <summary>上报无法完成时给出的原因。</summary>
    public string CannotCompleteReason { get; set; } = "缺少输入资料";

    /// <summary>端口节点连续提交两次取值，第二次用于验证重复提交被拒。</summary>
    public bool DoubleSubmitPorts { get; set; }

    /// <summary>持有期间忽略执行的取消，用于构造暂停与收口之间的竞态。</summary>
    public bool IgnoreCancellationWhileHeld { get; set; }

    /// <summary>按声明的端口生成提交文本：Text 语义端口给同名校验文本，Signal 端口只给端口名。</summary>
    private static string PortValuesFor(IReadOnlyList<NeutralExecutionOutputPort> ports)
    {
        string body = string.Join(", ", ports.Select(port =>
            $"\"{port.Name.Value}\": \"{(port.Kind == NeutralPortKind.Text ? $"{port.Name.Value}产出" : string.Empty)}\""));
        return "{ " + body + " }";
    }

    /// <summary>命中条件的执行返回模拟失败，用于验证失败后的重新执行。</summary>
    public Func<IExecution, bool>? FailsWhen { get; set; }

    /// <summary>本轮回复文本，经过程记录与过程播发送出，缺省为节点路径加产出。</summary>
    public Func<IExecution, string>? Reply { get; set; }

    /// <summary>本次执行期间写进过程记录的内容，用于断言记录与过程事件。</summary>
    internal Action<IExecution, RecordingJournal>? Emits { get; set; }

    /// <summary>提交类工具的返回文本，用于断言校验结果。</summary>
    public List<string> Submissions => [.. _submissions];

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _submissions = new();

    /// <summary>每个执行的观察点，用于断言上下文与执行顺序。</summary>
    public IReadOnlyList<IExecution> Started
    {
        get
        {
            lock (_gate)
            {
                return [.. _started];
            }
        }
    }

    /// <summary>执行器上报的模型请求用量，不设置时表示没有请求。</summary>
    public NeutralExecutionUsage Usage { get; set; } = NeutralExecutionUsage.None;

    /// <summary>执行器上报的恢复数据，未设置时不可恢复。</summary>
    public ExecutionRestorePayload? RestoreData { get; set; }

    /// <summary>各次执行收到的恢复数据，普通启动为空，按建立顺序。</summary>
    public IReadOnlyList<ExecutionRestorePayload?> Starts
    {
        get
        {
            lock (_gate)
            {
                return [.. _starts];
            }
        }
    }

    private readonly List<ExecutionRestorePayload?> _starts = [];

    public NeutralExecutionRunner Create(NeutralExecutionTarget target, NeutralExecutionId execution)
    {
        lock (_gate)
        {
            _starts.Add(null);
        }

        return new Run(this, execution);
    }

    public ErrorOr<NeutralExecutionRestoreData> Prepare(NeutralExecutionRestoreContext context)
    {
        lock (_gate)
        {
            _prepareCount++;
        }

        return new TestRestoreData(context.Payload);
    }

    /// <summary>准备过恢复计划的次数。</summary>
    public int PrepareCount
    {
        get
        {
            lock (_gate)
            {
                return _prepareCount;
            }
        }
    }

    private int _prepareCount;

    public NeutralExecutionRunner CreateRestored(
        NeutralExecutionRestoreContext context,
        NeutralExecutionRestoreData data)
    {
        lock (_gate)
        {
            _starts.Add(data is TestRestoreData restored
                ? new ExecutionRestorePayload(restored.Value.Format, restored.Value.Payload)
                : null);
        }

        return new Run(this, context.Execution);
    }

    private async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionId execution,
        NeutralExecutionRequest request,
        RecordingJournal journal,
        CancellationToken cancellationToken)
    {
        ExecutionIndex executions = Executions
            ?? throw new InvalidOperationException("测试环境没有提供执行索引。");
        IExecution started = (IExecution)executions.FindPayload(new Kuroe.Flow.Execution.ExecutionId(execution.Value))!;
        NodePath node = GraphRecordTargets.Parse(request.Target);

        lock (_gate)
        {
            _started.Add(started);
        }

        ExecutionHandle handle = Gate.Arrive(execution, node, request.Attempt);
        try
        {
            Emits?.Invoke(started, journal);

            Task opened = Gate.WaitOpen(handle);
            await (IgnoreCancellationWhileHeld ? opened : opened.WaitAsync(cancellationToken));

            IReadOnlyList<NeutralExecutionOutputPort> requiredPorts =
                [.. request.OutputPorts.Where(port => port.RequiresSubmission)];
            IReadOnlyList<ITool> contractTools = ContractToolFactory?.Invoke(execution) ?? [];
            if (requiredPorts.Count > 0 && SubmitsPorts)
            {
                string json = PortValuesJsonFor?.Invoke(started)
                    ?? (PortValuesJson.Length > 0 ? PortValuesJson : PortValuesFor(requiredPorts));
                ITool portTool = Tool(contractTools, ContractTools.Port);
                _submissions.Enqueue(Call(portTool, new Dictionary<string, object?> { ["valuesJson"] = json }));
                if (DoubleSubmitPorts)
                {
                    _submissions.Enqueue(Call(portTool, new Dictionary<string, object?> { ["valuesJson"] = json }));
                }
            }

            if (CannotComplete)
            {
                _submissions.Enqueue(Call(
                    Tool(contractTools, ContractTools.CannotComplete),
                    new Dictionary<string, object?> { ["reason"] = CannotCompleteReason }));
            }

            if (FailsWhen?.Invoke(started) == true)
            {
                return Error.Failure("模拟执行器失败");
            }

            string reply = Reply?.Invoke(started) ?? $"{node}产出";
            journal.AppendContent(ProcessContentKind.Reply, reply);

            return ExecutionOutput.Ports;
        }
        finally
        {
            Gate.Leave(handle);
        }
    }

    /// <summary>按模型调用工具的方式取回执文本，实参按参数名给出。</summary>
    private static string Call(ITool tool, Dictionary<string, object?> arguments) =>
        ToolResult.Render(tool.Functions.Single().Invoke(new ToolArguments(arguments)));

    /// <summary>按契约路径取工具。</summary>
    private static ITool Tool(IReadOnlyList<ITool> tools, string path) =>
        tools.Single(tool => tool.Functions.Any(function => function.Definition.Path.Value == path));

    /// <summary>一次执行的运行器：过程记录随执行建立，运行委托给共享的假执行器状态。</summary>
    private sealed class Run(FakeRunner owner, NeutralExecutionId execution)
        : NeutralExecutionRunner, NeutralExecutionUsageSource, NeutralExecutionRestoreDataSource
    {
        private readonly RecordingJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public NeutralExecutionUsage Usage => owner.Usage;

        public NeutralExecutionRestoreState? RestoreData =>
            owner.RestoreData is { } data
                ? new NeutralExecutionRestoreState(data.Format, data.Payload)
                : null;

        public event Action? RestoreDataChanged
        {
            add => _ = value;
            remove => _ = value;
        }

        public async Task<ErrorOr<NeutralExecutionOutput>> ExecuteAsync(
            NeutralExecutionRequest request,
            CancellationToken cancellationToken)
        {
            ErrorOr<ExecutionOutput> outcome = await owner.ExecuteAsync(
                new ExecutionId(execution.Value),
                request,
                _journal,
                cancellationToken).ConfigureAwait(false);

            return outcome.IsError
                ? outcome.ErrorsOrEmptyList
                : NeutralExecutionOutput.FromPorts([]);
        }
    }

    private sealed record TestRestoreData(NeutralExecutionRestoreState Value) : NeutralExecutionRestoreData;
}
