using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Maf.Control;
using Kuroe.Maf.Definition;
using Xunit;

namespace Kuroe.Host.FlowIntegration.Maf.Tests;

/// <summary>MAF 流程模板：模板带稳定实现标识与显示名，供同名多实现选择。</summary>
public sealed class MafFlowTemplatesTests
{
    [Fact(DisplayName = "MAF 模板带实现标识")]
    public void Maf_template_carries_the_implementation()
    {
        MafFlowDefinition definition = new(new FlowName("简单"), [], [], []);
        MafFlowTemplates templates = new(new MafFlowCatalog([definition]));

        FlowTemplateView view = Assert.Single(templates.All());

        Assert.Equal("maf", view.Implementation?.Value);
        Assert.Equal("MAF 流程", view.Engine);
    }
}