using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Ports;

/// <summary>任务开始端口的文本视图：固定值优先，其次宿主注入值，最后提交输入。
/// 动态 Text 端口与全部常量端口共用这一取值顺序，运行期开始节点与静态解析读取同一份结果。</summary>
public sealed class StartPortValues : IPortTexts
{
    /// <summary>按开始声明、宿主文本与提交输入解析全部承载文本的开始端口。</summary>
    public StartPortValues(StartNode start, IPortTexts hostTexts, StartInputs inputs)
    {
        Dictionary<PortName, string> texts = [];
        foreach (Port port in start.Outputs.Where(port => port.IsConstant || PortRules.CarriesText(port.Type)))
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
