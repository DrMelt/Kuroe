using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Node.Llm.Executions;
using Kuroe.Tool;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并记录并发峰值。</summary>
public sealed class FakeRunner : IExecutionRunner
{
    private readonly Lock _gate = new();
    private readonly List<IExecution> _started = [];
    private int _live;
    private int _peak;

    /// <summary>声明输出端口节点的交回通道，端口节点由此提交命名段。</summary>
    public PortSubmitter? PortSubmitter { get; set; }

    /// <summary>本轮状态的声明通道，所有执行节点都有这个工具。</summary>
    public StatusSubmitter? StatusSubmitter { get; set; }

    /// <summary>声明输出端口的执行节点交回的命名段。空时按执行声明的端口名逐个生成同名校验文本。</summary>
    public string PortValuesJson { get; set; } = string.Empty;

    /// <summary>按执行生成端口值 JSON，设置后优先于 <see cref="PortValuesJson"/> 与默认逐端口名生成；返回 null 时回退。用于按执行轮次区分端口内容的装配。</summary>
    public Func<IExecution, string?>? PortValuesJsonFor { get; set; }

    /// <summary>输出端口节点是否交回命名段，关掉用于验证未收口。</summary>
    public bool SubmitsPorts { get; set; } = true;

    /// <summary>是否声明本轮状态，关掉用于验证未声明时的表现。</summary>
    public bool SubmitsVerdict { get; set; } = true;

    /// <summary>声明的本轮状态文本，默认交付完毕。</summary>
    public string VerdictStatus { get; set; } = "completed";

    /// <summary>声明 blocked 时给出的原因。</summary>
    public string VerdictReason { get; set; } = string.Empty;

    /// <summary>端口节点连续提交两次取值，第二次用于验证重复提交被拒。</summary>
    public bool DoubleSubmitPorts { get; set; }

    /// <summary>单个执行的人为耗时，用于并发与取消断言。</summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>按执行差异化的人为耗时，未给定时统一用 <see cref="DelayMs"/>，用于推进时序断言。</summary>
    public Func<IExecution, int>? DelayFor { get; set; }

    /// <summary>按声明的端口名逐个生成同名校验文本，提交与声明一一对应。</summary>
    private static string PortValuesFor(IReadOnlyList<Port> ports)
    {
        string body = string.Join(", ", ports.Select(port => $"\"{port.Name.Value}\": \"{port.Name.Value}产出\""));
        return "{ " + body + " }";
    }

    /// <summary>命中条件的执行返回模拟失败，用于验证失败后的返工恢复。</summary>
    public Func<IExecution, bool>? FailsWhen { get; set; }

    /// <summary>本轮回复文本，经过程记录与过程播发送出，缺省为节点路径加产出。</summary>
    public Func<IExecution, string>? Reply { get; set; }

    /// <summary>本次执行期间写进过程记录与过程流的内容，用于断言记录与过程增量。</summary>
    internal Action<IExecution, ProcessJournal>? Emits { get; set; }

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

    public IExecutionBody CreateBody(ExecutionInput input) => new FakeBody();

    public async Task<ErrorOr<Success>> ExecuteAsync(IExecution execution, IExecutionBody body, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _started.Add(execution);
        }

        int now = Interlocked.Increment(ref _live);
        lock (_gate)
        {
            _peak = Math.Max(_peak, now);
        }

        try
        {
            Emits?.Invoke(execution, ((FakeBody)body).Journal);

            await Task.Delay(DelayFor?.Invoke(execution) ?? DelayMs, cancellationToken);

            IReadOnlyList<Port> textPorts = execution.Input.TextPorts;
            if (textPorts.Count > 0 && SubmitsPorts)
            {
                string json = PortValuesJsonFor?.Invoke(execution)
                    ?? (PortValuesJson.Length > 0 ? PortValuesJson : PortValuesFor(textPorts));
                _submissions.Enqueue(ToolResult.Render(PortSubmitter!.SubmitValues(execution.Id, json)));
                if (DoubleSubmitPorts)
                {
                    _submissions.Enqueue(ToolResult.Render(PortSubmitter.SubmitValues(execution.Id, json)));
                }
            }

            if (SubmitsVerdict)
            {
                _submissions.Enqueue(ToolResult.Render(StatusSubmitter!.SubmitVerdict(
                    execution.Id,
                    VerdictStatus,
                    VerdictReason.Length > 0 ? VerdictReason : null)));
            }

            if (FailsWhen?.Invoke(execution) == true)
            {
                return Error.Failure("模拟执行器失败");
            }

            string reply = Reply?.Invoke(execution) ?? $"{execution.Input.Node}产出";
            ((FakeBody)body).Journal.AppendText(reply);
            execution.Sink.OnText(reply);

            return Result.Success;
        }
        finally
        {
            Interlocked.Decrement(ref _live);
        }
    }

    /// <summary>测试用的执行体：只带过程记录，由假执行器自己写。</summary>
    private sealed class FakeBody : IExecutionBody
    {
        public ProcessJournal Journal { get; } = new();

        IProcessJournal IExecutionBody.Journal => Journal;
    }
}
