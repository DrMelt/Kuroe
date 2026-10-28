using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>已装配端口的文本表。静态端口视图、任务启动值、激活输入与执行输入共用这一读取面。</summary>
public interface IPortTexts
{
    /// <summary>已装配端口的文本，未装配的端口不在表中。</summary>
    IReadOnlyDictionary<PortName, string> Texts { get; }

    /// <summary>该端口的文本，未装配时为空。</summary>
    string? Text(PortName port) => Texts.GetValueOrDefault(port);
}