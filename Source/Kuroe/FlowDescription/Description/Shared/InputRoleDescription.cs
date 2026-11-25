namespace Kuroe.FlowDescription.Description.Shared;

/// <summary>输入端口执行角色的描述取值。未写角色时的默认值由模式与端口类型决定。</summary>
public enum InputRoleDescription
{
    /// <summary>作为执行内容参与本轮执行。</summary>
    Content,

    /// <summary>按名字供节点参数读取，不进入执行内容。</summary>
    Configuration,

    /// <summary>只驱动启动，不承载内容。</summary>
    Control,
}