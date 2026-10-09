using Kuroe.Shared.Workflows.Graph;
using Kuroe.Workflows.Flows;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个容器在任务内的运行时对象：容器定义与容器门控状态。容器不执行、不启动 run，
/// 放行与门控由 TaskRuntime 随成员推进刷新。读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeContainer(ContainerNode container, Func<int, RuntimeNode> resolve) : RuntimeNode(container)
{
    /// <summary>提交时锁定的容器定义，不可变。</summary>
    public ContainerNode Container { get; } = container;

    private readonly Dictionary<int, long> _publishedSources = [];
    private int _revision;
    private bool _awaiting;
    private bool _canceled;

    /// <summary>内容版本：任一直接来源发布新版本且容器重新齐备即递增，是容器作来源时与执行节点一致的版本账。</summary>
    public override long Revision => _revision;

    /// <summary>内容齐备：取消或任一直接成员、子容器未放行时不齐备，不受自身批准等待影响。</summary>
    public bool ContentReady =>
        !_canceled
        && Container.Members.All(member => resolve(member).Released)
        && Container.SubContainers.All(sub => resolve(sub).Released);

    /// <summary>产出已放行、可被下游消费：内容齐备且容器自身不等待批准。</summary>
    public override bool Released => ContentReady && !_awaiting;

    /// <summary>成员产出齐备后停在等待批准。</summary>
    public override bool Awaiting => _awaiting;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>内容齐备的发布落点：来源版本账有变化时记新账并推进内容版本，返回是否产生新版本。</summary>
    public bool PublishIfChanged(IReadOnlyDictionary<int, long> sources)
    {
        bool changed = sources.Count != _publishedSources.Count
            || sources.Any(pair => _publishedSources.GetValueOrDefault(pair.Key) != pair.Value);
        if (!changed)
        {
            return false;
        }

        _publishedSources.Clear();
        foreach ((int index, long revision) in sources)
        {
            _publishedSources[index] = revision;
        }

        _revision++;
        return true;
    }

    /// <summary>容器门控：产出齐备后停在等待批准。</summary>
    public void Park() => _awaiting = true;

    /// <summary>清除容器等待批准的状态。</summary>
    public void ClearAwaiting() => _awaiting = false;

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>已放行产出的位置集合：递归给容器内全部执行节点与子容器。</summary>
    public override IReadOnlyList<(int Node, int? Item)> ReleasedOutputs()
    {
        if (!Released)
        {
            return [];
        }

        return [.. Container.Members.SelectMany(member => resolve(member).ReleasedOutputs())
            .Concat(Container.SubContainers.SelectMany(sub => resolve(sub).ReleasedOutputs()))];
    }

    /// <summary>命名输出端口的产出文本：按端口绑定解析到成员，带端口取成员命名段，不带端口取成员整份。
    /// 成员未放行、端口未交回或绑定目标不是执行节点时为空。</summary>
    public override string? OutputText(WorkTask task, Flow.PortName? port)
    {
        if (port is not { } name
            || Container.PortBindings is not { } bindings
            || !bindings.TryGetValue(name, out Flow.NodeName target))
        {
            return null;
        }

        if (PortRef.Split(target) is { } memberRef
            && task.Graph.IndexOf(memberRef.Source) is { } memberIndex
            && resolve(memberIndex) is RuntimeExecutable member)
        {
            return member.OutputText(task, memberRef.Port);
        }

        return task.Graph.IndexOf(target) is { } wholeIndex && resolve(wholeIndex) is RuntimeExecutable whole
            ? whole.OutputText(task, port: null)
            : null;
    }
}
