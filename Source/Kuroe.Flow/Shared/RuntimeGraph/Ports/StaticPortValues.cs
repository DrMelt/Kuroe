using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Ports;

/// <summary>静态输入与常量输出的运行期取值：按编译期落定的结构终点取固定值、字面量、默认值、开始端口文本或运行期常量，
/// 并按节点给出静态输入与常量输出文本视图。运行期常量按需读取，未产出时不进入视图。</summary>
/// <remarks>按流程图、开始端口文本与运行期常量来源建立取值。</remarks>
/// <param name="graph">流程编译视图。</param>
/// <param name="startTexts">任务启动时确定的开始端口文本。</param>
/// <param name="runtime">运行期常量来源，未接任务运行状态时为空。</param>
public sealed class StaticPortValues(INodeGraph graph, IPortTexts startTexts, IRuntimeConstantSource? runtime = null)
{
    private readonly INodeGraph _graph = graph;
    private readonly IPortTexts _startTexts = startTexts;
    private readonly IRuntimeConstantSource? _runtime = runtime;

    /// <summary>执行节点的静态输入文本视图，未解析出的端口不在视图中。</summary>
    public IPortTexts For(ExecutableNode executable) =>
        new PortTexts(Resolve(executable.Path,
            executable.Inputs.Where(input => input.IsStatic).Select(input => input.Name), input: true));

    /// <summary>节点的常量输出文本视图，未解析出的端口不在视图中。</summary>
    public IPortTexts ConstantsFor(GraphNode node) =>
        new PortTexts(Resolve(node.Path,
            node.Outputs.Where(output => output.IsConstant).Select(output => output.Name), input: false));

    /// <summary>执行节点的静态输入是否全部可以取值：结构解析成功，运行期常量已经产出。</summary>
    public bool FullyResolved(ExecutableNode executable) =>
        executable.Inputs
            .Where(input => input.IsStatic)
            .All(input => Resolved(_graph.StaticInputResolution(executable.Path, input.Name)));

    /// <summary>解析结果是否可以取值，运行期常量以是否已产出判定。</summary>
    private bool Resolved(StaticResolution? resolution) =>
        resolution is { Error: null } found
        && (found.Kind != StaticSourceKind.RuntimeConstant
            || _runtime is not null && _runtime.Available(found.Node, found.Port));

    /// <summary>按节点与端口表解析文本，空值不进入结果。</summary>
    private Dictionary<PortName, string> Resolve(NodePath node, IEnumerable<PortName> ports, bool input)
    {
        Dictionary<PortName, string> texts = [];
        foreach (PortName port in ports)
        {
            StaticResolution? resolution = input
                ? _graph.StaticInputResolution(node, port)
                : _graph.ConstantResolution(node, port);
            if (Text(resolution) is { Length: > 0 } text)
            {
                texts[port] = text;
            }
        }

        return texts;
    }

    /// <summary>按结构终点取文本，无法取值时为空。</summary>
    private string? Text(StaticResolution? resolution) => resolution switch
    {
        null => null,
        { Kind: StaticSourceKind.StartFixed } found => found.Text,
        { Kind: StaticSourceKind.StartInjected } found => _startTexts.Text(found.Port),
        { Kind: StaticSourceKind.Literal } found => found.Text,
        { Kind: StaticSourceKind.RuntimeConstant } found => _runtime?.Text(found.Node, found.Port),
        { Kind: StaticSourceKind.Default } found => found.Text,
        _ => null,
    };
}

