using ErrorOr;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>流程查询工具：列出流程名与查看指定流程。宿主输入名取自宿主开始输入表。</summary>
public sealed class FlowKuroeInfoToolTests
{
    private static readonly FlowDefinition Flow = new(
        new FlowName("默认"),
        "示例",
        [new Port(new PortName("启动路径"), PortTypes.Text, mode: PortMode.Constant)],
        NodeSpec.Define(new NodeName("整体")));

    private static readonly IHostStartInputs HostInputs = new FakeHostStartInputs(
        new Dictionary<PortName, string> { [new PortName("启动路径")] = "WORKSPACE_PATH" });

    [Fact]
    public void ListFlows_renders_the_flow_name_and_description()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog(), HostInputs);

        string output = Call(tool, "ListFlows");

        Assert.Contains("默认", output, StringComparison.Ordinal);
        Assert.Contains("示例", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFlow_renders_the_expanded_flow()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog(), HostInputs);

        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("流程「默认」· 示例", output, StringComparison.Ordinal);
        Assert.Contains("开始", output, StringComparison.Ordinal);
    }

    /// <summary>开始端口的宿主输入名来自宿主开始输入表，端口名可以自定义。</summary>
    [Fact]
    public void GetFlow_shows_the_host_start_input_from_the_form()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog(), HostInputs);

        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("启动路径←WORKSPACE_PATH", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFlow_rejects_a_blank_name()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog(), HostInputs);

        Assert.Contains("缺少流程名", Call(tool, "GetFlow"), StringComparison.Ordinal);
    }

    private static string Call(FlowKuroeInfoTool tool, string name, params (string Name, object? Value)[] values) =>
        ToolResult.Render(tool.Functions.Single(function => function.Definition.Path.Name.Value == name)
            .Invoke(new ToolArguments(values.ToDictionary(value => value.Name, value => value.Value))));

    private sealed class FakeFlowCatalog : IFlowCatalog
    {
        public IReadOnlyList<FlowDefinition> All() => [Flow];

        public ErrorOr<FlowDefinition> Find(FlowName name) =>
            name == Flow.Name ? Flow : Error.NotFound("Flow.NotFound", $"没有流程 {name}。");
    }

    private sealed class FakeHostStartInputs(IReadOnlyDictionary<PortName, string> bindings) : IHostStartInputs
    {
        public IReadOnlyDictionary<PortName, string> Bindings(FlowName flow) =>
            flow == Flow.Name ? bindings : new Dictionary<PortName, string>();

        public ErrorOr<IReadOnlyDictionary<PortName, string>> Resolve(FlowDefinition flow) =>
            new Dictionary<PortName, string>(Bindings(flow.Name));
    }
}