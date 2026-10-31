using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Catalogs.Files;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Xunit;

namespace Kuroe.Tests.Support;

/// <summary>直接装配的模型选择依赖：偏好与目录落在临时目录，不建立任务与流程。</summary>
internal sealed class ModelServiceFixture : IDisposable
{
    private readonly string _root;

    private ModelServiceFixture(string root, SettingsProvider settings, CatalogService catalog, ModelService models)
    {
        _root = root;
        Settings = settings;
        Catalog = catalog;
        Models = models;
    }

    /// <summary>临时工作目录。</summary>
    public string Root => _root;

    public SettingsProvider Settings { get; }

    public CatalogService Catalog { get; }

    public ModelService Models { get; }

    /// <summary>在临时目录装配目录与偏好，默认注册并选中 fake 模型。</summary>
    public static ModelServiceFixture Create(bool selectModel = true, bool seedCatalog = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        KuroePaths paths = KuroePaths.At(root);
        Directory.CreateDirectory(paths.ConfigDirectory);

        SettingsProvider settings = Ok(SettingsProvider.Create(paths));
        CatalogService catalog = Ok(CatalogService.Create(new CatalogStore(paths.CatalogFile, root)));
        if (seedCatalog)
        {
            Ok(catalog.AddProvider("test", "https://example.invalid/v1", "key"));
            Ok(catalog.AddModel(ModelName.Create("fake").Value, ProviderName.Create("test").Value));
        }

        ModelService models = new(settings, catalog);
        if (selectModel && seedCatalog)
        {
            Ok(models.Select(ModelName.Create("fake").Value));
        }

        return new ModelServiceFixture(root, settings, catalog, models);
    }

    private static T Ok<T>(ErrorOr<T> result)
    {
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));

        return result.Value;
    }

    private static void Ok(ErrorOr<Success> result) =>
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}
