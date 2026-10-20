using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool;
using Kuroe.Tool.Contract;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并记录并发峰值。</summary>
public sealed class FakeRunner : IExecutionRunner
{
    private readonly Lock _gate = new();
    private readonly List<IExecution> _started = [];
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

    /// <summary>按声明的端口生成提交文本：Text 端口给同名校验文本，Signal 端口只给端口名。</summary>
    private static string PortValuesFor(IReadOnlyList<Port> ports)
    {
        string body = string.Join(", ", ports.Select(port =>
            $"\"{port.Name.Value}\": \"{(port.Type == PortType.Text ? $"{port.Name.Value}产出" : string.Empty)}\""));
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

            IReadOnlyList<Port> requiredPorts = execution.Input.RequiredPorts;
            if (requiredPorts.Count > 0 && SubmitsPorts)
            {
                string json = PortValuesJsonFor?.Invoke(execution)
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

    /// <summary>按模型调用工具的方式取回执文本：换一份绑定该执行的载体，实参按参数名给出。</summary>
    private static string Call(IScopedTool tool, IExecution execution, Dictionary<string, object?> arguments) =>
        ToolResult.Render(tool.ForExecution(execution.Id).Functions.Single().Invoke(new ToolArguments(arguments)));

    /// <summary>测试用的执行体：只带过程记录，由假执行器自己写。</summary>
    private sealed class FakeBody : IExecutionBody
    {
        public ProcessJournal Journal { get; } = new();

        IProcessJournal IExecutionBody.Journal => Journal;
    }
}
