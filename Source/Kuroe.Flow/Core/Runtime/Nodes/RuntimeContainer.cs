using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
namespace Kuroe.Flow.Runtime.Nodes;
/// <summary>一个容器在任务内的运行时对象：容器定义与最近一次发布。容器不派发执行器，按统一节点方案随成员与触发输入推进，每次发布计一次执行。
/// 状态查询只读已实例化节点，不创建节点：未实例化的成员视为没有产出。
/// 读写都要求持有任务 Gate。</summary>
internal sealed class RuntimeContainer(
    ContainerNode container,
    INodeContext context) : RuntimeNode(container)
{
    /// <summary>提交时锁定的容器定义，不可变。</summary>
    public ContainerNode Container { get; } = container;

    /// <summary>已实例化的直接子节点运行时对象。层级由容器自己持有，寻址从根逐段下钻。</summary>
    private readonly List<NodeRuntime> _children = [];

    private bool _canceled;

    /// <summary>已实例化的直接子节点，按登记顺序。</summary>
    public IReadOnlyList<NodeRuntime> Children => _children;

    /// <summary>登记一个直接子节点的运行时对象。</summary>
    public void Add(NodeRuntime child) => _children.Add(child);

    /// <summary>内容齐备：取消或任一内容来源未放行时不齐备。门控输入不参与已发布内容的可消费性。</summary>
    public bool ContentReady =>
        !_canceled
        && context.Plan(Path) is { } plan
        && plan.ContentBindings.All(binding => context.Source(binding.Source).Released);

    /// <summary>产出可消费：内容齐备。</summary>
    protected override bool Consumable => ContentReady;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>容器的一次执行：发布当前成员内容并捕获触发位。就绪与启动组消费由统一节点评估完成。</summary>
    public override void Activate(INodeContext context, INodeInputs inputs)
    {
        if (!ContentReady)
        {
            return;
        }

        Progress.Started();
        Progress.Publish(CreatePublication(Progress.NextRevision()));
    }

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>捕获容器的进展与发布。内容是否齐备由方案中的内容来源推导。</summary>
    public override NodeRecord? CaptureRecord() =>
        new ContainerNodeRecord(
            Path,
            CaptureProgress());

    /// <summary>从存档恢复容器的进展与发布。</summary>
    public override void RestoreRecord(NodeRecord record, INodeRestoreContext context)
    {
        if (record is not ContainerNodeRecord state)
        {
            throw new ArgumentException($"容器 {Path} 收到 {record.GetType().Name} 记录。", nameof(record));
        }

        RestoreProgress(state.Common);
    }

    /// <summary>按输出绑定找到成员来源。常量输出不进入发布，静态解析另行处理。</summary>
    private (NodeRuntime Node, PortName Port)? OutputSource(PortName port) =>
        Container.OutputBindings is { } bindings
        && bindings.TryGetValue(port, out OutputBinding target)
        && context.Materialized(target.Source) is { } source
            ? (source, target.Port)
            : null;

    /// <summary>按输出绑定构造一次发布：触发位取来源端口的触发值，动态输出转发成员内容，出处改指容器端口。
    /// 来源未放行或未声明时触发位按触发处理。</summary>
    private Publication CreatePublication(long revision)
    {
        Dictionary<PortName, bool> triggers = [];
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = [];
        foreach (Port port in Container.Outputs)
        {
            (NodeRuntime Node, PortName Port)? source = OutputSource(port.Name);
            if (port.Trigger)
            {
                bool released = source is { } found && found.Node.Released;
                bool fires = source is { } fire && fire.Node.Triggers(fire.Port);
                triggers[port.Name] = TriggerRules.Container(port, released, fires);
            }

            if (!port.IsConstant && source is { } actual && actual.Node.Released)
            {
                content[port.Name] = [.. actual.Node.OutputValues(actual.Port)
                    .Select(value => value with { Origin = new ContainerPortSource(Path, port.Name) })];
            }
        }

        return new Publication(revision, triggers, content);
    }

    /// <summary>容器在快照里的一刻状态：取消为取消，已发布且内容齐备为完成，其余为等待。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        NodeState state;
        if (_canceled)
        {
            state = NodeState.Canceled;
        }
        else if (Progress.Published && ContentReady)
        {
            state = NodeState.Done;
        }
        else
        {
            state = NodeState.Pending;
        }

        return new ExecutableStateSnapshot(Path, state);
    }
}