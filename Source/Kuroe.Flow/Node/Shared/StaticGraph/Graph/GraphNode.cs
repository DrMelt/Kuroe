using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.StaticGraph.Ports;

namespace Kuroe.Flow.Node.StaticGraph.Graph;

/// <summary>流程图上的节点实体：开始、执行节点或容器。以从根到本节点的路径标识，路径唯一性来自树位置。
/// 所有节点共用输入、输出与输出来源，具体节点只决定子节点与执行方式。</summary>
public abstract record GraphNode
{
    /// <summary>从根到本节点的路径，流程内唯一，是节点的标识。</summary>
    public required NodePath Path { get; init; }

    /// <summary>节点标签，路径的最后一段。</summary>
    public NodeLabel Label => Path.Label;

    /// <summary>输入端口表：所有节点共用。</summary>
    public virtual IReadOnlyList<InputPort> Inputs { get; init; } = [];

    /// <summary>输出端口表：所有节点共用。</summary>
    public virtual IReadOnlyList<Port> Outputs { get; init; } = [];

    /// <summary>输出端口到内部来源端口的绑定：来源是本节点输入或直接子节点输出。未声明来源时为空。</summary>
    public virtual IReadOnlyDictionary<PortName, OutputBinding>? OutputBindings { get; init; }

    /// <summary>输入端口到来源的绑定：执行节点是静态输入，容器是传入端口。未声明绑定时为空。</summary>
    public virtual IReadOnlyDictionary<PortName, InputBinding>? InputBindings { get; init; }

    /// <summary>节点自身的执行或发布次数上限，所有节点共用。容器每发布一次计一次。</summary>
    public virtual int MaxExecutions { get; init; } = int.MaxValue;

    /// <summary>以本节点为来源的绑定，编译期落定，按编译顺序。</summary>
    public IReadOnlyList<InputBinding> Outgoing { get; init; } = [];

    /// <summary>按名字取输入端口声明，未声明时为空。</summary>
    public InputPort? FindInput(PortName name) => Inputs.Find(name);

    /// <summary>按名字取输出端口声明，未声明时为空。</summary>
    public Port? FindPort(PortName name) => Outputs.Find(name);
}
