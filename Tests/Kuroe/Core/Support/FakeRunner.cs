using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool;
using Kuroe.Tool.Contract;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并记录并发峰值。</summary>
public sealed class FakeRunner : IExecutionRunnerFactory
{
    private readonly Lock _gate = new();
    private readonly List<IExecution> _started = [];

    /// <summary>执行索引，用于按执行号取执行对象供测试断言。</summary>
    public ExecutionIndex? Executions { get; set; }
    private int _live;
    private int _peak;

    /// <summary>交回端口产出的契约工具载体，按执行换一份。</summary>
    public PortTool? PortTool { get; set; }

    /// <summary>上报无法完成的契约工具载体，按执行换一份。</summary>
    public CannotCompleteTool? CannotCompleteTool { get; set; }

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

    /// <summary>单个执行的人为耗时，用于并发与取消断言。</summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>按执行差异化的人为耗时，未给定时统一用 <see cref="DelayMs"/>，用于推进时序断言。</summary>
    public Func<IExecution, int>? DelayFor { get; set; }

    /// <summary>按声明的端口生成提交文本：Text 语义端口给同名校验文本，Signal 端口只给端口名。</summary>
    private static string PortValuesFor(IReadOnlyList<Port> ports)
    {
        string body = string.Join(", ", ports.Select(port =>
            $"\"{port.Name.Value}\": \"{(Ports.CarriesText(port.Type) ? $"{port.Name.Value}产出" : string.Empty)}\""));
        return "{ " + body + " }";
    }

    /// <summary>命中条件的执行返回模拟失败，用于验证失败后的返工恢复。</summary>
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

    public int Peak { get { lock (_gate) { return _peak; } } }

    public IExecutionRunner Create(ExecutionId execution) => new Run(this, execution);

    private async Task<ErrorOr<Success>> ExecuteAsync(
        ExecutionId execution,
        ExecutionInput input,
        RecordingJournal journal,
        CancellationToken cancellationToken)
    {
        IExecution started = Executions!.Find(execution).ThrowIfError();

        lock (_gate)
        {
            _started.Add(started);
        }

        int now = Interlocked.Increment(ref _live);
        lock (_gate)
        {
            _peak = Math.Max(_peak, now);
        }

        try
        {
            Emits?.Invoke(started, journal);

            await Task.Delay(DelayFor?.Invoke(started) ?? DelayMs, cancellationToken);

            IReadOnlyList<Port> requiredPorts = input.RequiredPorts;
            if (requiredPorts.Count > 0 && SubmitsPorts)
            {
                string json = PortValuesJsonFor?.Invoke(started)
                    ?? (PortValuesJson.Length > 0 ? PortValuesJson : PortValuesFor(requiredPorts));
                _submissions.Enqueue(Call(PortTool!, execution, new Dictionary<string, object?> { ["valuesJson"] = json }));
                if (DoubleSubmitPorts)
                {
                    _submissions.Enqueue(Call(PortTool!, execution, new Dictionary<string, object?> { ["valuesJson"] = json }));
                }
            }

            if (CannotComplete)
            {
                _submissions.Enqueue(Call(CannotCompleteTool!, execution, new Dictionary<string, object?> { ["reason"] = CannotCompleteReason }));
            }

            if (FailsWhen?.Invoke(started) == true)
            {
                return Error.Failure("模拟执行器失败");
            }

            string reply = Reply?.Invoke(started) ?? $"{input.Node}产出";
            journal.AppendText(reply);

            return Result.Success;
        }
        finally
        {
            Interlocked.Decrement(ref _live);
        }
    }

    /// <summary>按模型调用工具的方式取回执文本：换一份绑定该执行的载体，实参按参数名给出。</summary>
    private static string Call(IScopedTool tool, ExecutionId execution, Dictionary<string, object?> arguments) =>
        ToolResult.Render(tool.ForExecution(execution).Functions.Single().Invoke(new ToolArguments(arguments)));

    /// <summary>一次执行的运行器：过程记录随执行建立，运行委托给共享的假执行器状态。</summary>
    private sealed class Run(FakeRunner owner, ExecutionId execution) : IExecutionRunner
    {
        private readonly RecordingJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public Task<ErrorOr<Success>> ExecuteAsync(ExecutionInput input, CancellationToken cancellationToken) =>
            owner.ExecuteAsync(execution, input, _journal, cancellationToken);
    }
}
