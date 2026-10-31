using ErrorOr;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>流程查询工具：列出流程名与查看指定流程。</summary>
public sealed class FlowKuroeInfoToolTests
{
    private static readonly FlowDefinition Flow = new(
        new FlowName("默认"),
        "示例",
        [],
        new Dictionary<PortName, string>(),
        NodeSpec.Define(new NodeName("整体")));

    [Fact]
    public void ListFlows_renders_the_flow_name_and_description()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog());

        string output = Call(tool, "ListFlows");

        Assert.Contains("默认", output, StringComparison.Ordinal);
        Assert.Contains("示例", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFlow_renders_the_expanded_flow()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog());

        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("流程「默认」· 示例", output, StringComparison.Ordinal);
        Assert.Contains("开始", output, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFlow_rejects_a_blank_name()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog());

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
}
