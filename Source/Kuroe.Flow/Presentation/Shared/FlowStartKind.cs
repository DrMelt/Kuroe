namespace Kuroe.Flow.Presentation;

/// <summary>开始端口的输入来源。</summary>
public enum FlowStartKind
{
    /// <summary>由用户提交输入。</summary>
    Content,

    /// <summary>流程定义固定值。</summary>
    Fixed,

    /// <summary>由宿主开始输入表提供。</summary>
    Host,

    /// <summary>不承载内容，自动放行。</summary>
    Automatic,
}