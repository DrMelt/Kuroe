using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;
using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>流程查询工具：列出流程名与查看指定流程。宿主输入名取自宿主开始输入表。</summary>
public sealed class FlowKuroeInfoToolTests
{
    private static readonly FlowTemplateView Flow = new(
        "默认",
        "示例",
        [
            new FlowStartPortView(
                "启动路径",
                "Text",
                null,
                null,
                null,
                FlowStartKind.Host,
                "WORKSPACE_PATH")
            {
                Constant = true,
            },
        ],
        [],
        []);

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

    /// <summary>开始端口的宿主输入名来自宿主开始输入表，端口名可以自定义。</summary>
    [Fact]
    public void GetFlow_shows_the_host_start_input_from_the_form()
    {
        FlowKuroeInfoTool tool = new(new FakeFlowCatalog());

        string output = Call(tool, "GetFlow", ("flowName", "默认"));

        Assert.Contains("启动路径←WORKSPACE_PATH", output, StringComparison.Ordinal);
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

    private sealed class FakeFlowCatalog : IFlowTemplateCatalog
    {
        public IReadOnlyList<FlowTemplateView> All() => [Flow];

        public ErrorOr<FlowTemplateView> Find(FlowName name) =>
            name.Value == Flow.Name ? Flow : Error.NotFound("Flow.NotFound", $"没有流程 {name}。");
    }
}