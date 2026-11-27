using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Control;

/// <summary>MAF 端口计划到展示端口的映射：种类取自计划，常量标记取自开始端口模式。开始端口的输入来源按提交要求与宿主绑定决定。</summary>
internal static class MafPortView
{
    internal static string Kind(ExecutionPortKind kind) => kind switch
    {
        ExecutionPortKind.Context => "Context",
        ExecutionPortKind.Signal => "Signal",
        _ => "Text",
    };

    internal static FlowPortView Start(MafStartPort port, string? text) =>
        new(port.Name.Value, Kind(port.Kind), text)
        {
            Constant = port.Mode == MafPortMode.Constant,
        };

    internal static FlowStartPortView Template(MafStartPort port, bool hostBound)
    {
        FlowStartKind kind = FlowStartKind.Content;
        if (!port.RequiresInput)
        {
            kind = FlowStartKind.Automatic;
        }
        else if (hostBound)
        {
            kind = FlowStartKind.Host;
        }

        return new(
            port.Name.Value,
            Kind(port.Kind),
            null,
            port.Default,
            null,
            kind,
            hostBound ? "当前模型" : null)
        {
            Constant = port.Mode == MafPortMode.Constant,
        };
    }

    internal static FlowPortView Output(MafOutputPort port) =>
        new(port.Name.Value, Kind(port.Kind), null, port.Trigger);
}