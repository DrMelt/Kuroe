using ErrorOr;
using Kuroe.Kernel.Control.Runtime;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Kernel.Tasks.Runtime.Tests;

/// <summary>流程模板目录：同一流程名可以按实现并列，按实现查找不返回其他实现。</summary>
public sealed class FlowTemplateCatalogTests
{
    [Fact(DisplayName = "同一流程名保留全部实现")]
    public void Same_name_keeps_each_implementation()
    {
        FlowTemplateCatalog catalog = new(
        [
            new Source(Template("默认", "graph")),
            new Source(Template("默认", "maf")),
        ]);

        Assert.Equal(2, catalog.All().Count);
        Assert.Equal(2, catalog.FindAll(new FlowName("默认")).Count);
        Assert.Equal("graph", catalog.Find(new FlowName("默认"), new FlowImplementation("graph")).Value.Engine);
        Assert.Equal("maf", catalog.Find(new FlowName("默认"), new FlowImplementation("maf")).Value.Engine);
    }

    [Fact(DisplayName = "同一实现的重复模板返回错误")]
    public void Duplicate_implementation_returns_an_error()
    {
        FlowTemplateCatalog catalog = new(
        [
            new Source(Template("默认", "graph")),
            new Source(Template("默认", "graph")),
        ]);

        Assert.True(catalog.Find(new FlowName("默认"), new FlowImplementation("graph")).IsError);
    }

    private static FlowTemplateView Template(string name, string implementation) =>
        new(name, null, [], [], [])
        {
            Engine = implementation,
            Implementation = new FlowImplementation(implementation),
        };

    private sealed class Source(params FlowTemplateView[] flows) : IFlowTemplateSource
    {
        public IReadOnlyList<FlowTemplateView> All() => flows;

        public ErrorOr<FlowTemplateView> Find(FlowName name)
        {
            FlowTemplateView? found = flows.FirstOrDefault(flow => flow.Name == name.Value);

            return found is null ? FlowErrors.FlowNotFound(name.Value) : found;
        }
    }
}