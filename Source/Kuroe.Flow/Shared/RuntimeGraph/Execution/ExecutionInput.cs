using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的输入：节点身份、轮次、本轮各输入端口取到的内容、执行可读的端口文本与节点声明。
/// 启动的执行只用这里声明的内容，不继承任何未声明的历史。输入端口文本统一经 <see cref="Texts"/> 读取。</summary>
public sealed record ExecutionInput : IPortTexts
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>所属执行节点的路径。</summary>
    public required NodePath Node { get; init; }

    /// <summary>本节点的类型描述，类型专属声明的取用口由类型自己给出。</summary>
    public required INodeType Type { get; init; }

    /// <summary>宿主对该执行节点的声明，引擎透传，未声明时为空。</summary>
    public INodeDeclaration? Declaration { get; init; }

    /// <summary>当前节点已启动的执行数，含本次，从 1 起。</summary>
    public int ExecutionCount { get; init; } = 1;

    /// <summary>本轮各输入端口取到的内容：Context 输入端口的取值在最前，其余按生效组内连接序，
    /// 未连接的默认值按输入声明序追加，信号输入不取内容。文本经 <see cref="Texts"/> 读取。</summary>
    public IReadOnlyList<InputValue> Inputs { get; init; } = [];

    /// <summary>本轮执行可读的端口文本表：动态值、静态值与默认值都已装配，未声明的端口不在其中。</summary>
    public IReadOnlyDictionary<PortName, string> Texts { get; init; } = new Dictionary<PortName, string>();

    /// <summary>某个输入端口的文本，未装配时为空。</summary>
    public string? Text(PortName port) => Texts.GetValueOrDefault(port);

    /// <summary>节点声明的输出端口，未声明时为空。</summary>
    public IReadOnlyList<Port> OutputPorts { get; init; } = [];

    /// <summary>本轮需交回的命名端口：声明为 Text 或 Signal 的输出端口，未声明时为空。</summary>
    public IReadOnlyList<Port> RequiredPorts => PortRules.RequiringSubmission(OutputPorts);
}
