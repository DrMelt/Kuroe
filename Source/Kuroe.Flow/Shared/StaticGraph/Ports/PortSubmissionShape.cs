namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>输出端口交回值的形态：普通端口交回文本，触发端口交回对象；承载内容的端口在值里带文本，触发端口在对象里带触发位。</summary>
public enum PortSubmissionShape
{
    /// <summary>普通端口，交回文本并保留内容。</summary>
    Content,

    /// <summary>普通端口，交回文本但不承载内容。</summary>
    Signal,

    /// <summary>触发端口，交回带 text 与 trigger 的对象。</summary>
    TriggeredContent,

    /// <summary>触发端口，交回只带 trigger 的对象。</summary>
    TriggeredSignal,
}

/// <summary>端口交回形态的判定：由端口模式与内容语义给出，schema 与解析共用同一组判断。</summary>
public static class PortSubmissionShapes
{
    /// <summary>端口的交回形态。触发端口交回对象，普通端口交回文本；对象里是否带 text 由端口是否承载内容决定。</summary>
    public static PortSubmissionShape SubmissionShape(this Port port) =>
        (port.Trigger, PortRules.CarriesContent(port.Type)) switch
        {
            (true, true) => PortSubmissionShape.TriggeredContent,
            (true, false) => PortSubmissionShape.TriggeredSignal,
            (false, true) => PortSubmissionShape.Content,
            (false, false) => PortSubmissionShape.Signal,
        };

    /// <summary>交回值是否为对象。</summary>
    public static bool IsObject(this PortSubmissionShape shape) =>
        shape is PortSubmissionShape.TriggeredContent or PortSubmissionShape.TriggeredSignal;

    /// <summary>交回值是否带文本。</summary>
    public static bool IncludesText(this PortSubmissionShape shape) =>
        shape is PortSubmissionShape.Content or PortSubmissionShape.TriggeredContent;

    /// <summary>交回值是否带触发位。</summary>
    public static bool IncludesTrigger(this PortSubmissionShape shape) =>
        shape is PortSubmissionShape.TriggeredContent or PortSubmissionShape.TriggeredSignal;
}