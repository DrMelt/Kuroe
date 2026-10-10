using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.TaskExecution.Tasks;

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

    /// <summary>按输出端口取单段文本：声明的命名端口按绑定转发成员产出，其余端口为空。成员未放行或端口未交回时为空。</summary>
    public override string? PortText(WorkTask task, Flow.PortName port)
    {
        if (Container.PortBindings is not { } bindings
            || !bindings.TryGetValue(port, out Flow.PortRef target)
            || target.Source is not { } memberName
            || task.Graph.IndexOf(memberName) is not { } memberIndex
            || resolve(memberIndex) is not RuntimeExecutable member)
        {
            return null;
        }

        return member.PortText(task, target.Port);
    }

    /// <summary>按输出端口取可注入的产出消息集：声明的输出端口按绑定转成一条容器端口消息，出处指向容器。
    /// 容器没有整份产出，一切对外内容都经声明端口。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(WorkTask task, Flow.PortName port, int? item)
    {
        if (item is not null)
        {
            return [];
        }

        if (Released
            && Container.PortBindings is { } bindings
            && bindings.TryGetValue(port, out Flow.PortRef target)
            && target.Source is { } memberName
            && task.Graph.IndexOf(memberName) is { } memberIndex
            && resolve(memberIndex) is RuntimeExecutable member)
        {
            string? content = member.PortText(task, target.Port);
            if (content is { Length: > 0 })
            {
                return [new ContextMessage(MessageRole.User,
                    $"节点「{Name}」的端口「{port}」产出：\n{content}",
                    new ContainerPortSource(Name, port))];
            }
        }

        return [];
    }
}
