using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Maf.Definition;

namespace Kuroe.Maf.Control;

/// <summary>MAF 端口计划到展示端口的映射：种类取自计划，常量标记取自开始端口模式。</summary>
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

    internal static FlowStartPortView Template(MafStartPort port) =>
        new(port.Name.Value, Kind(port.Kind), null, port.Default, null, FlowStartKind.Content, null)
        {
            Constant = port.Mode == MafPortMode.Constant,
        };

    internal static FlowPortView Output(MafOutputPort port) =>
        new(port.Name.Value, Kind(port.Kind), null, port.Trigger);
}