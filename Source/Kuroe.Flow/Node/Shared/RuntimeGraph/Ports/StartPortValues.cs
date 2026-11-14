using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;

/// <summary>任务开始端口的文本视图：固定值优先，其次宿主给值，最后提交输入。
/// 只有承载内容的开始端口进入视图，运行期开始来源与静态解析读取同一份结果。</summary>
public sealed class StartPortValues : IPortTexts
{
    /// <summary>按开始端口声明、宿主文本与提交输入解析承载内容的开始端口。</summary>
    public StartPortValues(IReadOnlyList<Port> ports, IPortTexts hostTexts, StartInputs inputs)
    {
        Dictionary<PortName, string> texts = [];
        foreach (Port port in ports.Where(port => PortRules.CarriesContent(port.Type)))
        {
            if ((port.Value ?? hostTexts.Text(port.Name) ?? inputs.Text(port.Name)) is { Length: > 0 } text)
            {
                texts[port.Name] = text;
            }
        }

        Texts = texts;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<PortName, string> Texts { get; }

    /// <inheritdoc/>
    public string? Text(PortName port) => Texts.GetValueOrDefault(port);
}
