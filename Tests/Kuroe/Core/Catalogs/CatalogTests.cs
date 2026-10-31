using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Shared.Catalogs;
using Kuroe.Tests.Support;
using Xunit;

namespace Kuroe.Tests.Catalogs;

/// <summary>目录的增删、凭据更换、脱敏导出与合并导入。</summary>
public sealed class CatalogTests
{
    [Fact]
    public void Add_model_registers_it_for_selection()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        fixture.Catalog.AddModel(ModelName.Create("m2").Value, ProviderName.Create("test").Value).ThrowIfError();

        Assert.True(fixture.Models.IsRegistered(ModelName.Create("m2").Value));
        fixture.Models.Select(ModelName.Create("m2").Value).ThrowIfError();
        Assert.Equal("m2", fixture.Models.Current?.Value);
    }

    [Fact]
    public void Duplicate_provider_is_rejected()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<Success> added = fixture.Catalog.AddProvider("test", "https://example.invalid/v2", "k2");

        Assert.True(added.IsError);
    }

    [Fact]
    public void Provider_still_referenced_by_a_model_is_refused()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<Success> removed = fixture.Catalog.RemoveProvider("test");

        Assert.True(removed.IsError);
        Assert.Contains(fixture.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "test");
    }

    [Fact]
    public void Unreferenced_provider_can_be_removed()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();
        fixture.Catalog.AddProvider("备用", "https://example.invalid/v2", "k2").ThrowIfError();

        fixture.Catalog.RemoveProvider("备用").ThrowIfError();

        Assert.DoesNotContain(fixture.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_key_replaces_the_credentials()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        fixture.Catalog.SetProviderKey("test", "新凭据").ThrowIfError();

        ProviderDefinition provider = fixture.Catalog.Snapshot().Providers
            .Single(candidate => candidate.ProviderName.Value == "test");
        Assert.Equal("新凭据", provider.ApiKey.Value);
    }

    [Fact]
    public void Export_masks_credentials_and_import_merges_them()
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();
        string target = Path.Combine(fixture.Root, "catalog-meta.json");

        string exported = fixture.Catalog.Export(target).ThrowIfError();

        Assert.True(File.Exists(exported));
        string text = File.ReadAllText(exported);
        Assert.Contains("***", text);

        using ModelServiceFixture fresh = ModelServiceFixture.Create(seedCatalog: false);
        CatalogMerge merged = fresh.Catalog.Import(exported).ThrowIfError();

        Assert.Equal("test", Assert.Single(fresh.Catalog.Snapshot().Providers).ProviderName.Value);
        Assert.Empty(merged.Notes);

        string relative = fixture.Catalog.Export("meta.json").ThrowIfError();
        Assert.True(Path.IsPathRooted(relative));
        Assert.True(File.Exists(relative));
    }

    [Theory]
    [InlineData("catalog", "CatalogFile.OverwritesCatalog")]
    [InlineData("invalid", "CatalogFile.InvalidPath")]
    public void Invalid_export_target_is_rejected(string target, string expected)
    {
        using ModelServiceFixture fixture = ModelServiceFixture.Create();

        ErrorOr<string> exported = target == "catalog"
            ? fixture.Catalog.Export(KuroePaths.At(fixture.Root).CatalogFile)
            : fixture.Catalog.Export("bad\u0000path");

        Assert.True(exported.IsError);
        Assert.Equal(expected, exported.FirstError.Code);
    }
}
