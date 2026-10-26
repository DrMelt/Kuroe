using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>任务启动时宿主开始端口给出的值：端口名到文本。</summary>
/// <remarks>按端口建立宿主值表。</remarks>
public sealed class HostStartValues(IReadOnlyDictionary<PortName, string> values)
{
    private readonly IReadOnlyDictionary<PortName, string> _values = values;

    /// <summary>按端口取宿主值，未给出时为空。</summary>
    public string? Text(PortName port) => _values.GetValueOrDefault(port);
}