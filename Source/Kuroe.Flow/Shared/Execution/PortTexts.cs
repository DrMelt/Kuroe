using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>固定的端口文本表：按端口给出已装配的文本。静态端口视图与宿主开始值共用这一实现。</summary>
/// <param name="texts">端口到文本的表。</param>
public sealed class PortTexts(IReadOnlyDictionary<PortName, string> texts) : IPortTexts
{
    /// <summary>没有端口值的文本表。</summary>
    public static PortTexts Empty { get; } = new(new Dictionary<PortName, string>());

    /// <inheritdoc/>
    public IReadOnlyDictionary<PortName, string> Texts { get; } = texts;

    /// <inheritdoc/>
    public string? Text(PortName port) => Texts.GetValueOrDefault(port);
}
