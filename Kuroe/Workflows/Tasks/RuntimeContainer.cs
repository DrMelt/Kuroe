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

    private bool _produced;
    private bool _awaiting;
    private bool _canceled;

    /// <summary>成员产出曾齐备过，广播只发一次，作废复位后重新广播。</summary>
    public bool Produced => _produced;

    /// <summary>齐备代数：从非齐备到齐备每推进一次递增，是容器作来源时的新语义版本账。</summary>
    public override long Revision => _revision;
    private int _revision;

    /// <summary>产出已放行、可被下游消费：容器自身不等待批准、不取消，且直接成员与子容器都放行。</summary>
    public override bool Released =>
        !_awaiting && !_canceled
        && Container.Members.All(member => resolve(member).Released)
        && Container.SubContainers.All(sub => resolve(sub).Released);

    /// <summary>成员产出齐备后停在等待批准。</summary>
    public override bool Awaiting => _awaiting;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>成员产出齐备的刷新落点，首次触发时置位并推进齐备代数。</summary>
    public void MarkProduced()
    {
        if (_produced)
        {
            return;
        }

        _produced = true;
        _revision++;
    }

    /// <summary>成员作废使产出不再齐备时复位。</summary>
    public void ResetProduced() => _produced = false;

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
