using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Host.Composition.Catalogs;
using Kuroe.Host.Composition.Catalogs.Files;
using Kuroe.Host.Composition.Configuration;
using Kuroe.Host.Composition.Configuration.Files;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Host.Composition.Tests.Support;

/// <summary>直接装配的模型选择依赖：偏好与目录落在内存文件访问，不建立任务与流程。</summary>
internal sealed class ModelServiceFixture
{
    private ModelServiceFixture(
        string root,
        InMemoryWorkspaceFiles files,
        SettingsProvider settings,
        CatalogService catalog,
        ModelService models)
    {
        Root = root;
        Files = files;
        Settings = settings;
        Catalog = catalog;
        Models = models;
    }

    /// <summary>工作目录根。</summary>
    public string Root { get; }

    /// <summary>内存文件访问，用例经它读写工作目录内的文件。</summary>
    public InMemoryWorkspaceFiles Files { get; }

    public SettingsProvider Settings { get; }

    public CatalogService Catalog { get; }

    public ModelService Models { get; }

    /// <summary>在内存文件访问上装配目录与偏好，默认注册并选中 fake 模型。</summary>
    public static ModelServiceFixture Create(bool selectModel = true, bool seedCatalog = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        KuroePaths paths = KuroePaths.At(root);
        InMemoryWorkspaceFiles files = new();
        files.AddDirectory(paths.ConfigDirectory);

        SettingsProvider settings = Ok(SettingsProvider.Create(paths, files));
        CatalogService catalog = Ok(CatalogService.Create(new CatalogStore(paths.CatalogFile, root, files)));
        if (seedCatalog)
        {
            Ok(catalog.AddProvider("test", "https://example.invalid/v1", "key"));
            Ok(catalog.AddModel(
                ModelName.Create("fake").Value,
                ProviderName.Create("test").Value,
                ProviderModelName.Create("fake").Value));
        }

        ModelService models = new(settings, catalog);
        if (selectModel && seedCatalog)
        {
            Ok(models.Select(ModelName.Create("fake").Value));
        }

        return new ModelServiceFixture(root, files, settings, catalog, models);
    }

    private static T Ok<T>(ErrorOr<T> result)
    {
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));

        return result.Value;
    }

    private static void Ok(ErrorOr<Success> result) =>
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));
}
