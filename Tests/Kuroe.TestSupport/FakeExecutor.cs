using ErrorOr;
using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Shared.Agent;
using Kuroe.Workflows.Tasks;

namespace Kuroe.TestSupport;

/// <summary>不接模型的执行器：按角色交回条目或结论，并记录并发峰值。</summary>
public sealed class FakeExecutor : IRunExecutor
{
    private readonly Lock _gate = new();
    private readonly List<AgentRun> _started = [];
    private int _live;
    private int _peak;

    public UnitSubmitter? Submitter { get; set; }

    /// <summary>规划步骤交回的条目拆分。</summary>
    public string ItemsJson { get; set; } = """
        [
          { "Title": "甲", "Instruction": "做甲", "Acceptance": "甲可见" },
          { "Title": "乙", "Instruction": "做乙", "Acceptance": "乙可见" }
        ]
        """;

    /// <summary>检查步骤是否交回通过，默认全部通过。</summary>
    public Func<AgentRun, bool> CheckPasses { get; set; } = _ => true;

    /// <summary>规划步骤是否交回条目，关掉用于验证未收口。</summary>
    public bool SubmitsPlan { get; set; } = true;

    /// <summary>单个 agent 的人为耗时，用于并发与取消断言。</summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>非规划与检查步骤的返回文本，缺省为角色名加产出。</summary>
    public Func<AgentRun, string>? Output { get; set; }

    /// <summary>提交类工具的返回文本，用于断言校验结果。</summary>
    public List<string> Submissions => [.. _submissions];

    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _submissions = new();

    /// <summary>每个 agent 的观察点，用于断言上下文与执行顺序。</summary>
    public IReadOnlyList<AgentRun> Started
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

    public async Task<ErrorOr<string>> ExecuteAsync(AgentRun run, CancellationToken cancellationToken)
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
            await Task.Delay(DelayMs, cancellationToken);

            if (run.Context.Role == RunRole.Plan && SubmitsPlan)
            {
                _submissions.Enqueue(Submitter!.SubmitPlan(run.Scope, ItemsJson));
            }

            if (run.Context.Role == RunRole.Check)
            {
                bool passed = CheckPasses(run);
                _submissions.Enqueue(Submitter!.SubmitVerdict(run.Scope, passed, passed ? string.Empty : "验收项未满足"));
            }

            return Output?.Invoke(run) ?? $"{run.Context.Role.Label()}产出";
        }
        finally
        {
            Interlocked.Decrement(ref _live);
        }
    }
}
