using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
namespace Kuroe.Flow.Runtime.Nodes;
/// <summary>一个容器在任务内的运行时对象：容器定义与内容版本。容器不执行、不启动执行，
/// 内容齐备与内容版本按输入侧方案随成员推进刷新。状态查询只读已实例化节点，不创建节点：未实例化的成员视为没有产出。
/// 读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeContainer(
    ContainerNode container,
    INodeContext context,
    IConsumedRevisionStore? consumed = null) : RuntimeNode(container), IInputProgress
{
    /// <summary>提交时锁定的容器定义，不可变。</summary>
    public ContainerNode Container { get; } = container;

    /// <summary>已实例化的直接子节点运行时对象。层级由容器自己持有，寻址从根逐段下钻。</summary>
    private readonly List<NodeRuntime> _children = [];

    private readonly IConsumedRevisionStore _consumed = consumed ?? new ConsumedRevisionStore();
    private int _revision;
    private bool _canceled;

    /// <summary>已实例化的直接子节点，按登记顺序。</summary>
    public IReadOnlyList<NodeRuntime> Children => _children;

    /// <summary>登记一个直接子节点的运行时对象。</summary>
    public void Add(NodeRuntime child) => _children.Add(child);

    /// <summary>输入侧方案：一组绑定覆盖全部直接成员与子容器。</summary>
    private NodeInputPlan Plan => context.Plan(Path)!;

    /// <summary>内容版本：任一直接来源有未消费的新版本且容器重新齐备即递增，是容器作来源时与执行节点一致的产出版本。</summary>
    public override long Revision => _revision;

    /// <summary>内容齐备：取消或任一直接成员、子容器未放行时不齐备。未实例化的成员没有产出。</summary>
    public bool ContentReady => !_canceled && Plan.Available(context);

    /// <summary>产出已放行、可被下游消费：内容齐备。</summary>
    public override bool Released => ContentReady;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>内容变化的发布落点：成员有未消费的新版本且内容齐备时登记这些版本并推进内容版本，返回是否产生新版本。</summary>
    public bool PublishIfChanged()
    {
        if (!Plan.Ready(this, context))
        {
            return false;
        }

        _consumed.Record(Plan, context);
        _revision++;

        return true;
    }

    long IInputProgress.ConsumedRevision(ConsumptionKey key) => _consumed.Revision(key);

    void IInputProgress.Consume(InputGroup group, IReadinessSources sources) => _consumed.Record(group, sources);

    int IInputProgress.Executions => 0;

    bool IInputProgress.Published => _revision > 0;

    bool IInputProgress.ForceRerun => false;

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>捕获容器的内容版本与已消费版本。内容是否齐备由成员状态推导。</summary>
    public override NodeCheckpoint? CaptureCheckpoint() =>
        new ContainerNodeCheckpoint(
            Path,
            new NodeCommonCheckpoint(Revision, LastInputGroup, _consumed.CaptureCheckpoint(), [], false, false));

    /// <summary>从检查点恢复容器的内容版本与已消费版本。</summary>
    public override void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context)
    {
        if (checkpoint is not ContainerNodeCheckpoint state)
        {
            throw new ArgumentException($"容器 {Path} 收到 {checkpoint.GetType().Name} 检查点。", nameof(checkpoint));
        }

        RestoreLastInputGroup(state.Common.LastInputGroup);
        _consumed.RestoreCheckpoint(state.Common.Consumed);
        _revision = (int)state.Common.Revision;
    }

    /// <summary>按输出端口取单段文本：动态端口按绑定转发成员产出。常量端口由静态解析读取，不进入运行时。</summary>
    public override string? PortText(INodeContext context, PortName port) =>
        DynamicMemberOf(port) is { } member
            ? member.Node.PortText(context, member.Port)
            : null;

    /// <summary>按输出端口取产出：动态端口按绑定转发成员取值，出处改指容器的命名端口。常量端口不进入运行时。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Released && DynamicMemberOf(port) is { } member
            ? [.. member.Node.OutputValues(context, member.Port)
                .Select(value => value with { Origin = new ContainerPortSource(Path, port) })]
            : [];

    /// <summary>输出端口绑定到的成员运行期对象与成员端口，常量端口、未声明该端口或成员未实例化时为空。</summary>
    private (NodeRuntime Node, PortName Port)? DynamicMemberOf(PortName port) =>
        Container.FindPort(port) is { } declared && !declared.IsConstant
        && Container.OutputBindings is { } bindings
        && bindings.TryGetValue(port, out NodePort target)
        && context.Materialized(target.Node) is { } member
            ? (member, target.Port)
            : null;

    /// <summary>按输出端口配置转发成员端口的触发值：端口不承载触发、未放行或未声明来源时按触发处理。</summary>
    public override bool Triggers(PortName port)
    {
        if (Released && DynamicMemberOf(port) is { } member)
        {
            return TriggerRules.Container(Container.FindPort(port), true, member.Node.Triggers(member.Port));
        }

        return TriggerRules.Container(Container.FindPort(port), false, false);
    }
}
