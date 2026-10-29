namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>输入端口在节点执行中的角色。角色由节点执行契约决定，与端口来源是常量还是动态无关。</summary>
public enum InputRole
{
    /// <summary>作为执行内容参与本轮执行。</summary>
    Content,

    /// <summary>按名字供节点参数读取，不进入执行内容。</summary>
    Configuration,

    /// <summary>只驱动启动，不承载内容。</summary>
    Control,
}
