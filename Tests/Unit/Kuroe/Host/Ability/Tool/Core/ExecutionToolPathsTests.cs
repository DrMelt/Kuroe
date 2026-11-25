using Xunit;
using Kuroe.Kernel.Execution.Shared.Ports;

namespace Kuroe.Host.Ability.Tool.Tests;

/// <summary>执行工具路径：声明的白名单在前，按输出端口附上交回能力，所有执行都带上报与查看通道。</summary>
public sealed class ExecutionToolPathsTests
{
    [Fact(DisplayName = "声明排在最前其后是能力路径与契约工具")]
    public void Declaration_leads_the_capability_paths_and_contract_tools_follow() =>
        Assert.Equal(
            ["ReadFile", "SubmitPortValues", "ReportCannotComplete", "GetStatus"],
            ExecutionToolPaths.For([new ToolPath("ReadFile")], [Port("结论", ExecutionPortKind.Text)])
                .Select(path => path.Value));

    [Fact(DisplayName = "文本端口授予端口契约工具")]
    public void Text_ports_grant_the_port_contract_tool() =>
        Assert.Equal(
            ["SubmitPortValues", "ReportCannotComplete", "GetStatus"],
            ExecutionToolPaths.For(Array.Empty<ToolPath>(), [Port("结论", ExecutionPortKind.Text)])
                .Select(path => path.Value));

    [Fact(DisplayName = "信号端口授予端口契约工具")]
    public void Signal_ports_grant_the_port_contract_tool() =>
        Assert.Equal(
            ["SubmitPortValues", "ReportCannotComplete", "GetStatus"],
            ExecutionToolPaths.For(Array.Empty<ToolPath>(), [Port("完成", ExecutionPortKind.Signal)])
                .Select(path => path.Value));

    [Fact(DisplayName = "上下文端口保留执行通道")]
    public void Context_ports_keep_the_execution_channels() =>
        Assert.Equal(
            ["ReportCannotComplete", "GetStatus"],
            ExecutionToolPaths.For(Array.Empty<ToolPath>(), [Port("上下文", ExecutionPortKind.Context)])
                .Select(path => path.Value));

    [Fact(DisplayName = "没有输出端口的节点保留执行通道")]
    public void A_node_without_output_ports_keeps_the_execution_channels() =>
        Assert.Equal(
            ["ReportCannotComplete", "GetStatus"],
            ExecutionToolPaths.For(Array.Empty<ToolPath>(), []).Select(path => path.Value));

    [Fact(DisplayName = "声明路径接受文本形式")]
    public void Declared_paths_accept_the_textual_form() =>
        Assert.Equal(
            ["file/read/ReadFile", "ReportCannotComplete", "GetStatus"],
            ExecutionToolPaths.For(["file/read/ReadFile"], []).Select(path => path.Value));

    private static ExecutionOutputPort Port(string name, ExecutionPortKind kind) =>
        new(new ExecutionPortName(name), kind, kind != ExecutionPortKind.Context, false);
}