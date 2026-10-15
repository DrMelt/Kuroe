using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Flow.RuntimeGraph;
namespace Kuroe.Flow.RuntimeNodes;

using Flow = Kuroe.Flow.Shared.StaticGraph;

/// <summary>一个容器在任务内的运行时对象：容器定义与内容版本。容器不执行、不启动 run，
/// 内容齐备与内容版本按输入侧方案随成员推进刷新。状态查询只读已实例化节点，不创建节点：未实例化的成员视为没有产出。
/// 读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeContainer(ContainerNode container, INodeContext context) : NodeRuntime(container), IInputProgress
{
    /// <summary>提交时锁定的容器定义，不可变。</summary>
    public ContainerNode Container { get; } = container;

    private readonly ConsumptionLedger _consumed = new();
    private int _revision;
    private bool _canceled;

    /// <summary>输入侧方案：一组绑定覆盖全部直接成员与子容器。</summary>
    private NodeInputPlan Plan => context.Plan(Index)!;

    /// <summary>内容版本：任一直接来源发布新版本且容器重新齐备即递增，是容器作来源时与执行节点一致的版本账。</summary>
    public override long Revision => _revision;

    /// <summary>内容齐备：取消或任一直接成员、子容器未放行时不齐备。未实例化的成员没有产出。</summary>
    public bool ContentReady => !_canceled && NodeReadiness.Available(Plan, null, context);

    /// <summary>产出已放行、可被下游消费：内容齐备。</summary>
    public override bool Released => ContentReady;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>内容变化的发布落点：成员版本相对消费账有变化且内容齐备时记新账并推进内容版本，返回是否产生新版本。</summary>
    public bool PublishIfChanged()
    {
        if (NodeReadiness.Ready(Plan, this, context).Count == 0)
        {
            return false;
        }

        _consumed.Record(Plan, context);
        _revision++;

        return true;
    }

    IReadOnlyList<int>? IInputProgress.AdoptedItems => null;

    long IInputProgress.ConsumedRevision(int source) => _consumed.Revision(source);

    bool IInputProgress.ConsumedOnce(int source) => _consumed.ConsumedOnce(source);

    bool IInputProgress.Produced(int? item) => false;

    int IInputProgress.Runs(int? item) => 0;

    bool IInputProgress.ForceRerun => false;

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>按输出端口取单段文本：声明的命名端口按绑定转发成员产出，其余端口为空。成员未实例化、未放行或端口未交回时为空。</summary>
    public override string? PortText(INodeTask task, Flow.PortName port)
    {
        if (Container.PortBindings is not { } bindings
            || !bindings.TryGetValue(port, out Flow.PortRef target)
            || target.Source is not { } memberName
            || task.Graph.IndexOf(memberName) is not { } memberIndex
            || context.Materialized(memberIndex) is not NodeRuntime member)
        {
            return null;
        }

        return member.PortText(task, target.Port);
    }

    /// <summary>按输出端口取可注入的产出消息集：声明的输出端口按绑定转成一条容器端口消息，出处指向容器。
    /// 容器没有整份产出，一切对外内容都经声明端口。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(INodeTask task, Flow.PortName port, int? item)
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
            && context.Materialized(memberIndex) is NodeRuntime member)
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
