using ErrorOr;
using Kuroe.Executions;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Tasks;

namespace Kuroe.TestSupport;

/// <summary>不接模型的执行器：按节点产出要求交回条目或结论，并记录并发峰值。</summary>
public sealed class FakeExecutor : IRunExecutor
{
    private readonly Lock _gate = new();
    private readonly List<Run> _started = [];
    private int _live;
    private int _peak;

    public PlanSubmitter? Submitter { get; set; }

    /// <summary>声明输出端口节点的交回通道，端口节点由此提交命名段。</summary>
    public PortSubmitter? PortSubmitter { get; set; }

    /// <summary>规划执行节点交回的条目拆分。</summary>
    public string ItemsJson { get; set; } = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
        ]
        """;

    /// <summary>声明输出端口的执行节点交回的命名段。空时按 run 声明的端口名逐个生成同名校验文本。</summary>
    public string PortValuesJson { get; set; } = string.Empty;

    /// <summary>按 run 生成端口值 JSON，设置后优先于 <see cref="PortValuesJson"/> 与默认逐端口名生成；返回 null 时回退。用于按执行轮次区分端口内容的装配。</summary>
    public Func<Run, string?>? PortValuesJsonFor { get; set; }

    /// <summary>规划执行节点是否交回条目，关掉用于验证未收口。</summary>
    public bool SubmitsPlan { get; set; } = true;

    /// <summary>输出端口节点是否交回命名段，关掉用于验证未收口。</summary>
    public bool SubmitsPorts { get; set; } = true;

    /// <summary>端口节点连续提交两次取值，第二次用于验证重复提交被拒。</summary>
    public bool DoubleSubmitPorts { get; set; }

    /// <summary>单个 run 的人为耗时，用于并发与取消断言。</summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>按 run 差异化的人为耗时，未给定时统一用 <see cref="DelayMs"/>，用于推进时序断言。</summary>
    public Func<Run, int>? DelayFor { get; set; }

    /// <summary>按声明的端口名逐个生成同名校验文本，提交与声明一一对应。</summary>
    private static string PortValuesFor(IReadOnlyList<PortName> ports)
    {
        string body = string.Join(", ", ports.Select(port => $"\"{port.Value}\": \"{port.Value}产出\""));
        return "{ " + body + " }";
    }

    /// <summary>命中条件的 run 返回模拟失败，用于验证失败后的返工恢复。</summary>
    public Func<Run, bool>? FailsWhen { get; set; }

    /// <summary>非规划执行节点的返回文本，缺省为契约名加产出。</summary>
    public Func<Run, string>? Output { get; set; }

    /// <summary>提交类工具的返回文本，用于断言校验结果。</summary>
    public List<string> Submissions => [.. _submissions];

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _submissions = new();

    /// <summary>每个 run 的观察点，用于断言上下文与执行顺序。</summary>
    public IReadOnlyList<Run> Started
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

    public async Task<ErrorOr<string>> ExecuteAsync(Run run, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _started.Add(run);
        }

        int now = Interlocked.Increment(ref _live);
        lock (_gate)
        {
            _peak = Math.Max(_peak, now);
        }

        try
        {
            await Task.Delay(DelayFor?.Invoke(run) ?? DelayMs, cancellationToken);

            if (run.Context.Output == NodeOutput.Plan && SubmitsPlan)
            {
                _submissions.Enqueue(ToolResult.Render(Submitter!.SubmitItems(run.Scope, ItemsJson)));
            }

            if (run.Context.OutputPorts.Count > 0 && SubmitsPorts)
            {
                string json = PortValuesJsonFor?.Invoke(run)
                    ?? (PortValuesJson.Length > 0 ? PortValuesJson : PortValuesFor(run.Context.OutputPorts));
                _submissions.Enqueue(ToolResult.Render(PortSubmitter!.SubmitValues(run.Scope, json)));
                if (DoubleSubmitPorts)
                {
                    _submissions.Enqueue(ToolResult.Render(PortSubmitter.SubmitValues(run.Scope, json)));
                }
            }

            if (FailsWhen?.Invoke(run) == true)
            {
                return Error.Failure("模拟执行器失败");
            }

            return Output?.Invoke(run) ?? $"{run.Context.Output.Label()}产出";
        }
        finally
        {
            Interlocked.Decrement(ref _live);
        }
    }
}
