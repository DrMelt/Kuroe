using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Ports;

/// <summary>静态端口的运行期解析：按结构终点取固定值、默认值或注入的开始端口文本，并按执行节点给出静态端口文本视图。</summary>
public sealed class StaticPortValues
{
    private readonly INodeGraph _graph;
    private readonly IPortTexts _startTexts;
    private readonly IReadOnlyDictionary<NodePath, IPortTexts> _byExecutable;

    /// <summary>按流程图与注入的开始端口文本建立解析，并预解析全部执行节点的静态文本。</summary>
    /// <param name="graph">流程编译视图。</param>
    /// <param name="startTexts">注入的开始端口文本。</param>
    public StaticPortValues(INodeGraph graph, IPortTexts startTexts)
    {
        _graph = graph;
        _startTexts = startTexts;
        _byExecutable = graph.ExecutableNodes.ToDictionary(
            executable => executable.Path,
            executable => (IPortTexts)new PortTexts(Resolve(executable)));
    }

    /// <summary>执行节点的静态端口文本视图，未收录时为空视图。</summary>
    public IPortTexts For(ExecutableNode executable) =>
        _byExecutable.GetValueOrDefault(executable.Path) ?? PortTexts.Empty;

    /// <summary>解析执行节点全部静态输入端口的文本，空值不进入结果。</summary>
    private Dictionary<PortName, string> Resolve(ExecutableNode executable)
    {
        Dictionary<PortName, string> texts = [];
        foreach (PortName port in executable.Inputs.Where(input => input.IsStatic).Select(input => input.Name))
        {
            if (Text(executable.Path, port) is { Length: > 0 } text)
            {
                texts[port] = text;
            }
        }

        return texts;
    }

    /// <summary>按节点与端口取静态端口文本，不属于静态端口或无法解析时为空。</summary>
    private string? Text(NodePath node, PortName port)
    {
        StaticResolution resolution = StaticSources.Resolve(_graph, node, port);

        return resolution.Kind switch
        {
            StaticSourceKind.StartFixed => resolution.Text,
            StaticSourceKind.StartInjected => _startTexts.Text(resolution.Port),
            StaticSourceKind.Default => resolution.Text,
            _ => null,
        };
    }
}