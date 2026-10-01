using Kuroe.Shared.Workflows.Graph;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个容器在任务内的运行时对象：容器定义与容器门控状态。容器不执行、不派 run，
/// 放行与门控由 TaskFlow 随成员推进刷新。读写都要求持有任务 Gate。</summary>
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
    public override long Version => _generation;
    private int _generation;

    /// <summary>产出已放行、可被下游消费：容器自身不停批不取消，且直接成员与子容器都放行。</summary>
    public override bool Released =>
        !_awaiting && !_canceled
        && Container.Members.All(member => resolve(member).Released)
        && Container.SubContainers.All(sub => resolve(sub).Released);

    /// <summary>成员产出齐备后停在等人批准。</summary>
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
        _generation++;
    }

    /// <summary>成员作废使产出不再齐备时复位。</summary>
    public void ResetProduced() => _produced = false;

    /// <summary>容器门控：产出齐备后停在等人批准。</summary>
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
}