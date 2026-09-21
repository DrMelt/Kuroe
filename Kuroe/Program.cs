using ErrorOr;
using Kuroe;
using Kuroe.Agent;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Microsoft.Extensions.DependencyInjection;

KuroePaths paths = KuroePaths.Resolve();

ErrorOr<CatalogService> catalog = CatalogService.Create(new CatalogStore(paths.CatalogFile));
if (catalog.IsError)
{
    Errors.Report(catalog.ErrorsOrEmptyList);
    return 1;
}

ErrorOr<SettingsProvider> settings = SettingsProvider.Create(paths);
if (settings.IsError)
{
    Errors.Report(settings.ErrorsOrEmptyList);
    return 1;
}

var services = new ServiceCollection();
ErrorOr<Success> agent = services.AddKuroeAgent(settings.Value, catalog.Value);
if (agent.IsError)
{
    Errors.Report(agent.ErrorsOrEmptyList);
    return 1;
}

using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

await Repl.RunAsync(
    provider.GetRequiredService<AgentSession>(),
    settings.Value,
    catalog.Value);

return 0;
