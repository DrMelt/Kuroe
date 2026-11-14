namespace Kuroe.Tasks.Shared;

/// <summary>按端口读取的文本表，任务的开始端口文本与流程视图共用。</summary>
public sealed class FlowTexts(IReadOnlyDictionary<PortName, string> texts)
{
    /// <summary>空文本表。</summary>
    public static FlowTexts Empty { get; } = new(new Dictionary<PortName, string>());

    /// <summary>全部端口文本。</summary>
    public IReadOnlyDictionary<PortName, string> Texts { get; } = texts;

    /// <summary>按端口取文本，未给出时为空。</summary>
    public string? Text(PortName port) => Texts.GetValueOrDefault(port);
}