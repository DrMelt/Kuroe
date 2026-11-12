using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>开始端口提交规则：提交通道承载内容，只有承载内容的开始端口接受提交。
/// 是否必填由固定值、宿主绑定与 Default 决定，不承载内容的开始端口不进入提交。</summary>
public static class StartInputRules
{
    /// <summary>该开始端口在提交中的处理方式。</summary>
    public static StartInputKind Kind(Port port, bool hostBound)
    {
        if (port.Value is not null)
        {
            return StartInputKind.Fixed;
        }

        if (hostBound)
        {
            return StartInputKind.Host;
        }

        return PortRules.CarriesContent(port.Type) ? StartInputKind.Input : StartInputKind.Automatic;
    }

    /// <summary>该开始端口能否接受提交输入。</summary>
    public static bool AcceptsInput(Port port, bool hostBound) =>
        Kind(port, hostBound) == StartInputKind.Input;

    /// <summary>该开始端口是否必须由提交给出输入。</summary>
    public static bool RequiresInput(Port port, bool hostBound) =>
        AcceptsInput(port, hostBound) && port.Default is null;
}

/// <summary>开始端口在提交中的处理方式。</summary>
public enum StartInputKind
{
    /// <summary>带固定值的只读端口。</summary>
    Fixed,

    /// <summary>由宿主开始输入表给值的只读端口。</summary>
    Host,

    /// <summary>不承载内容，任务启动时自动放行，不进入提交。</summary>
    Automatic,

    /// <summary>由提交输入给值的端口。</summary>
    Input,
}