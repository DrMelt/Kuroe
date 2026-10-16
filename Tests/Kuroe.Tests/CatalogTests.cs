using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Shared.Catalogs;
using Xunit;

namespace Kuroe.Tests;

/// <summary>目录的增删、凭据更换、脱敏导出与合并导入。</summary>
public sealed class CatalogTests
{
    [Fact]
    public void Add_model_registers_it_for_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        environment.Catalog.AddModel(ModelName.Create("m2").Value, ProviderName.Create("test").Value).ThrowIfError();

        Assert.True(environment.Models.IsRegistered(ModelName.Create("m2").Value));
        environment.Models.Select(ModelName.Create("m2").Value).ThrowIfError();
        Assert.Equal("m2", environment.Models.Current?.Value);
    }

    [Fact]
    public void Duplicate_provider_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<Success> added = environment.Catalog.AddProvider("test", "https://example.invalid/v2", "k2");

        Assert.True(added.IsError);
    }

    [Fact]
    public void Provider_still_referenced_by_a_model_is_refused()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<Success> removed = environment.Catalog.RemoveProvider("test");

        Assert.True(removed.IsError);
        Assert.Contains(environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "test");
    }

    [Fact]
    public void Unreferenced_provider_can_be_removed()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Catalog.AddProvider("备用", "https://example.invalid/v2", "k2").ThrowIfError();

        environment.Catalog.RemoveProvider("备用").ThrowIfError();

        Assert.DoesNotContain(environment.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_key_replaces_the_credentials()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        environment.Catalog.SetProviderKey("test", "新凭据").ThrowIfError();

        ProviderDefinition provider = environment.Catalog.Snapshot().Providers
            .Single(candidate => candidate.ProviderName.Value == "test");
        Assert.Equal("新凭据", provider.ApiKey.Value);
    }

    [Fact]
    public void Export_masks_credentials_and_import_merges_them()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        string target = Path.Combine(environment.Root, "catalog-meta.json");

        string exported = environment.Catalog.Export(target).ThrowIfError();

        Assert.True(File.Exists(exported));
        string text = File.ReadAllText(exported);
        Assert.Contains("***", text);

        using TestEnvironment fresh = TestEnvironment.Create(seedCatalog: false);
        CatalogMerge merged = fresh.Catalog.Import(exported).ThrowIfError();

        Assert.Equal("test", Assert.Single(fresh.Catalog.Snapshot().Providers).ProviderName.Value);
        Assert.Empty(merged.Notes);
    }

    [Fact]
    public void Export_onto_the_catalog_file_is_refused()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<string> exported = environment.Catalog.Export(KuroePaths.At(environment.Root).CatalogFile);

        Assert.True(exported.IsError);
        Assert.Equal("CatalogFile.OverwritesCatalog", exported.FirstError.Code);
    }

    [Fact]
    public void Export_accepts_a_relative_path_against_the_work_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        string path = environment.Catalog.Export("meta.json").ThrowIfError();

        Assert.True(Path.IsPathRooted(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Invalid_export_path_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        ErrorOr<string> exported = environment.Catalog.Export("bad\u0000path");

        Assert.True(exported.IsError);
        Assert.Equal("CatalogFile.InvalidPath", exported.FirstError.Code);
    }
}
