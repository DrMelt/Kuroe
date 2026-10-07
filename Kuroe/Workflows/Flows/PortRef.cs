namespace Kuroe.Workflows.Flows;

/// <summary>端口引用语法 来源@端口 的解析：无端口时为空。节点名与端口名由各自校验保证不混用 @。</summary>
internal static class PortRef
{
    /// <summary>拆一个端口引用。以第一个 @ 为界，边界处（@ 开头或结尾）不作为引用。</summary>
    public static (Kuroe.Shared.Workflows.Flows.NodeName Source, Kuroe.Shared.Workflows.Flows.PortName Port)? Split(Kuroe.Shared.Workflows.Flows.NodeName name)
    {
        int at = name.Value.IndexOf('@');
        if (at > 0 && at < name.Value.Length - 1)
        {
            return (new Kuroe.Shared.Workflows.Flows.NodeName(name.Value[..at]), new Kuroe.Shared.Workflows.Flows.PortName(name.Value[(at + 1)..]));
        }

        return null;
    }
}
