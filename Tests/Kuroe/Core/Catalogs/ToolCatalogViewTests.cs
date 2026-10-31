using Kuroe.Catalogs;
using Kuroe.Shared.Catalogs;
using Kuroe.Tests.Support;
using Kuroe.Tool;
using Kuroe.Tool.KuroeInfo;
using Xunit;

namespace Kuroe.Tests.Catalogs;

/// <summary>目录工具视图：提供商与模型取自同一份快照，凭据只报是否已设置。</summary>
public sealed class ToolCatalogViewTests
{
    [Fact]
    public void Read_reports_providers_models_and_selection_without_credentials()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();
        ToolCatalogView view = new(fixture.Catalog, fixture.Models);

        CatalogSummary summary = view.Read();

        ProviderInfo provider = Assert.Single(summary.Providers);
        Assert.Equal("test", provider.Name);
        Assert.Equal("https://example.invalid/v1", provider.Endpoint);
        Assert.True(provider.CredentialSet);
        ModelInfo model = Assert.Single(summary.Models);
        Assert.Equal("fake", model.Name);
        Assert.Equal("test", model.Provider);
        Assert.Equal("fake", summary.CurrentModel);

        CatalogKuroeInfoTool tool = new(view);
        string output = ToolResult.Render(Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?>())));

        Assert.Contains("test", output, StringComparison.Ordinal);
        Assert.Contains("fake", output, StringComparison.Ordinal);
        Assert.Contains("凭据已设置", output, StringComparison.Ordinal);
        Assert.DoesNotContain("key", output, StringComparison.Ordinal);
    }
}
