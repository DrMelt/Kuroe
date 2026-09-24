using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Shared.Catalogs;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tests;

/// <summary>目录的增删、凭据更换、脱敏导出与合并导入。</summary>
public sealed class CatalogTests
{
    [Fact]
    public void Add_model_registers_it_for_selection()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        harness.Catalog.AddModel("m2", "test").ThrowIfError();

        Assert.True(harness.Models.IsRegistered("m2"));
        harness.Models.Select("m2").ThrowIfError();
        Assert.Equal("m2", harness.Models.Current);
    }

    [Fact]
    public void Duplicate_provider_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<Success> added = harness.Catalog.AddProvider("test", "https://example.invalid/v2", "k2");

        Assert.True(added.IsError);
    }

    [Fact]
    public void Provider_still_referenced_by_a_model_is_refused()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<Success> removed = harness.Catalog.RemoveProvider("test");

        Assert.True(removed.IsError);
        Assert.Contains(harness.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "test");
    }

    [Fact]
    public void Unreferenced_provider_can_be_removed()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Catalog.AddProvider("备用", "https://example.invalid/v2", "k2").ThrowIfError();

        harness.Catalog.RemoveProvider("备用").ThrowIfError();

        Assert.DoesNotContain(harness.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "备用");
    }

    [Fact]
    public void Provider_key_replaces_the_credentials()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        harness.Catalog.SetProviderKey("test", "新凭据").ThrowIfError();

        ProviderDefinition provider = harness.Catalog.Snapshot().Providers
            .Single(candidate => candidate.ProviderName.Value == "test");
        Assert.Equal("新凭据", provider.ApiKey.Value);
    }

    [Fact]
    public void Export_masks_credentials_and_import_merges_them()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        string target = Path.Combine(harness.Root, "catalog-meta.json");

        string exported = harness.Catalog.Export(target).ThrowIfError();

        Assert.True(File.Exists(exported));
        string text = File.ReadAllText(exported);
        Assert.Contains("***", text);

        using KuroeHarness fresh = KuroeHarness.Create(seedCatalog: false);
        CatalogMerge merged = fresh.Catalog.Import(exported).ThrowIfError();

        Assert.Equal("test", Assert.Single(fresh.Catalog.Snapshot().Providers).ProviderName.Value);
        Assert.Empty(merged.Notes);
    }

    [Fact]
    public void Export_onto_the_catalog_file_is_refused()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<string> exported = harness.Catalog.Export(Path.Combine(harness.Root, "catalog.json"));

        Assert.True(exported.IsError);
        Assert.Equal("CatalogFile.OverwritesCatalog", exported.FirstError.Code);
    }

    [Fact]
    public void Export_accepts_a_relative_path_against_the_work_directory()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        string path = harness.Catalog.Export("meta.json").ThrowIfError();

        Assert.True(Path.IsPathRooted(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Invalid_export_path_is_rejected()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        ErrorOr<string> exported = harness.Catalog.Export("bad\u0000path");

        Assert.True(exported.IsError);
        Assert.Equal("CatalogFile.InvalidPath", exported.FirstError.Code);
    }
}