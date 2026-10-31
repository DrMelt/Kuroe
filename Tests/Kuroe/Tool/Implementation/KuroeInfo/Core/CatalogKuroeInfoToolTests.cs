using Kuroe.Shared.Catalogs;
using Xunit;

namespace Kuroe.Tool.KuroeInfo.Tests;

/// <summary>目录查询工具：空目录提示、提供商与模型的清单和当前选择。</summary>
public sealed class CatalogKuroeInfoToolTests
{
    [Fact]
    public void Empty_catalog_prints_the_registration_hints()
    {
        CatalogKuroeInfoTool tool = new(new FakeCatalog(new CatalogSummary([], [], null)));

        string output = Call(tool);

        Assert.Contains("还没有提供商", output, StringComparison.Ordinal);
        Assert.Contains("还没有注册模型", output, StringComparison.Ordinal);
        Assert.Contains("当前模型：未选择", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_lists_providers_models_and_the_current_selection()
    {
        CatalogKuroeInfoTool tool = new(new FakeCatalog(new CatalogSummary(
            [new ProviderInfo("test", "https://example.invalid/v1", true)],
            [new ModelInfo("fake", "test")],
            "fake")));

        string output = Call(tool);

        Assert.Contains("test · https://example.invalid/v1 · 凭据已设置", output, StringComparison.Ordinal);
        Assert.Contains("fake → test", output, StringComparison.Ordinal);
        Assert.Contains("当前模型：fake", output, StringComparison.Ordinal);
    }

    private static string Call(CatalogKuroeInfoTool tool) =>
        ToolResult.Render(Assert.Single(tool.Functions).Invoke(new ToolArguments(new Dictionary<string, object?>())));

    private sealed class FakeCatalog(CatalogSummary summary) : ICatalogView
    {
        public CatalogSummary Read() => summary;
    }
}
