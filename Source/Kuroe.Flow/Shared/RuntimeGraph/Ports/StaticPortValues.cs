using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Ports;

/// <summary>静态输入与常量输出的运行期解析：按结构终点取固定值、字面量、默认值或注入的开始端口文本，
/// 并按节点给出静态输入与常量输出文本视图。</summary>
public sealed class StaticPortValues
{
    private readonly INodeGraph _graph;
    private readonly IPortTexts _startTexts;
    private readonly IReadOnlyDictionary<NodePath, IPortTexts> _byExecutable;
    private readonly IReadOnlyDictionary<NodePath, IPortTexts> _constants;

    /// <summary>按流程图与注入的开始端口文本建立解析，并预解析全部执行节点的静态文本与全部节点的常量输出文本。</summary>
    /// <param name="graph">流程编译视图。</param>
    /// <param name="startTexts">注入的开始端口文本。</param>
    public StaticPortValues(INodeGraph graph, IPortTexts startTexts)
    {
        _graph = graph;
        _startTexts = startTexts;
        _byExecutable = graph.ExecutableNodes.ToDictionary(
            executable => executable.Path,
            executable => (IPortTexts)new PortTexts(ResolveInputs(executable)));
        _constants = graph.Nodes.ToDictionary(
            node => node.Path,
            node => (IPortTexts)new PortTexts(ResolveConstants(node)));
    }

    /// <summary>执行节点的静态输入文本视图，未收录时为空视图。</summary>
    public IPortTexts For(ExecutableNode executable) =>
        _byExecutable.GetValueOrDefault(executable.Path) ?? PortTexts.Empty;

    /// <summary>节点的常量输出文本视图，未收录时为空视图。</summary>
    public IPortTexts ConstantsFor(GraphNode node) =>
        _constants.GetValueOrDefault(node.Path) ?? PortTexts.Empty;

    /// <summary>解析执行节点全部静态输入端口的文本，空值不进入结果。</summary>
    private Dictionary<PortName, string> ResolveInputs(ExecutableNode executable)
    {
        Dictionary<PortName, string> texts = [];
        foreach (PortName port in executable.Inputs.Where(input => input.IsStatic).Select(input => input.Name))
        {
            if (Text(executable.Path, port, StaticPortSide.Input) is { Length: > 0 } text)
            {
                texts[port] = text;
            }
        }

        return texts;
    }

    /// <summary>解析节点全部常量输出端口的文本，空值不进入结果。</summary>
    private Dictionary<PortName, string> ResolveConstants(GraphNode node)
    {
        Dictionary<PortName, string> texts = [];
        foreach (PortName port in node.Outputs.Where(output => output.IsConstant).Select(output => output.Name))
        {
            if (Text(node.Path, port, StaticPortSide.Output) is { Length: > 0 } text)
            {
                texts[port] = text;
            }
        }

        return texts;
    }

    /// <summary>按节点与端口取文本，不属于该侧端口或无法解析时为空。</summary>
    private string? Text(NodePath node, PortName port, StaticPortSide side)
    {
        StaticResolution resolution = side == StaticPortSide.Input
            ? StaticSources.ResolveInput(_graph, node, port)
            : StaticSources.ResolveOutput(_graph, node, port);

        return resolution.Kind switch
        {
            StaticSourceKind.StartFixed => resolution.Text,
            StaticSourceKind.StartInjected => _startTexts.Text(resolution.Port),
            StaticSourceKind.Literal => resolution.Text,
            StaticSourceKind.Default => resolution.Text,
            _ => null,
        };
    }

    private enum StaticPortSide
    {
        Input,
        Output,
    }
}
