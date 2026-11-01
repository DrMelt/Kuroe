using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>节点输入侧的运行期进展：已消费版本、执行次数、上次启动组、重跑意图与最近一次发布。
/// 发布是不可变快照，进展由节点在发布与作废点改动，读写都要求持有任务 Gate。</summary>
public sealed class InputProgress : IInputProgress
{
    private readonly Dictionary<ConsumptionKey, long> _consumed = [];

    /// <summary>最近一次发布的版本，作废后不回退。</summary>
    public long Revision { get; private set; }

    /// <summary>已启动、已交付或已发布的次数。</summary>
    public int Executions { get; private set; }

    /// <summary>上一次激活使用的输入侧组序号，强制重跑按它装配输入。</summary>
    public int? LastInputGroup { get; private set; }

    /// <summary>返工作废后要求越过就绪条件重启一次。</summary>
    public bool ForceRerun { get; private set; }

    /// <summary>最近一次发布，未发布或作废后为空。</summary>
    public Publication? Publication { get; private set; }

    /// <summary>是否已经发布过产出。</summary>
    public bool Published => Publication is not null;

    /// <summary>已消费版本的登记。</summary>
    public IReadOnlyDictionary<ConsumptionKey, long> Consumed => _consumed;

    /// <summary>下一次发布的版本。</summary>
    public long NextRevision() => Revision + 1;

    /// <summary>记一次启动或交付。</summary>
    public void Started() => Executions++;

    /// <summary>记下本次激活使用的输入侧组序号。</summary>
    public void RecordGroup(int group) => LastInputGroup = group;

    /// <summary>登记一次发布，版本取发布的版本。</summary>
    public void Publish(Publication publication)
    {
        Revision = publication.Revision;
        Publication = publication;
    }

    /// <summary>作废发布，版本与执行次数保持不变。</summary>
    public void Invalidate() => Publication = null;

    /// <summary>记下重跑意图。</summary>
    public void RequestRerun() => ForceRerun = true;

    /// <summary>重跑意图已消耗。</summary>
    public void ClearRerun() => ForceRerun = false;

    /// <inheritdoc/>
    public long ConsumedRevision(ConsumptionKey key) => _consumed.GetValueOrDefault(key);

    /// <inheritdoc/>
    public void Consume(InputGroup group, IReadinessSources sources)
    {
        foreach (InputBinding binding in group.Bindings)
        {
            _consumed[binding.Key] = sources.Source(binding.Source).Revision;
        }
    }

    /// <summary>按检查点回填版本、次数、上次启动组、重跑意图、已消费版本与发布。</summary>
    public void Restore(
        long revision,
        int executions,
        int? lastInputGroup,
        bool rerun,
        IEnumerable<KeyValuePair<ConsumptionKey, long>> consumed,
        Publication? publication)
    {
        Revision = revision;
        Executions = executions;
        LastInputGroup = lastInputGroup;
        ForceRerun = rerun;
        Publication = publication;
        _consumed.Clear();
        foreach ((ConsumptionKey key, long value) in consumed)
        {
            _consumed[key] = value;
        }
    }
}
