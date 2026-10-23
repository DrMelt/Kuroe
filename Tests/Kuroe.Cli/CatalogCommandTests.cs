using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>目录、提供商与模型命令。</summary>
public sealed class CatalogCommandTests
{
    [Fact]
    public async Task Export_then_import_reports_merges()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        StringWriter output = new();
        StringWriter error = new();
        CatalogCommands command = new(environment.Session, new CliOutput(output, error));

        int exported = command.Export("exported.json");

        Assert.Equal(ExitCodes.Success, exported);
        Assert.True(File.Exists(Path.Combine(environment.Root, "exported.json")));
        Assert.Contains("已导出到", output.ToString());

        int imported = command.Import("exported.json");

        Assert.Equal(ExitCodes.Success, imported);
        Assert.Contains("合并导入", output.ToString());
        Assert.Contains("未导入", error.ToString());
    }

    [Fact]
    public async Task Provider_and_model_lifecycle()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (ProviderCommands providers, ModelCommands models, StringWriter output, StringWriter error) = Build(environment);

        Assert.Equal(ExitCodes.Success, providers.Add("p2", "https://example.invalid/v2", "k2"));
        Assert.Contains(environment.Session.Catalog.Snapshot().Providers,
            provider => provider.ProviderName.Value == "p2");

        Assert.Equal(ExitCodes.Success, providers.Key("p2", "k3"));
        Assert.Equal("k3", environment.Session.Catalog.Snapshot().Providers
            .Single(provider => provider.ProviderName.Value == "p2").ApiKey.Value);

        Assert.Equal(ExitCodes.Success, models.Add("m2", "p2"));
        Assert.Equal(ExitCodes.Success, models.Use("m2"));
        Assert.Equal("m2", environment.Session.Models.Current?.Value);

        Assert.Equal(ExitCodes.Success, models.Remove("m2"));
        Assert.Null(environment.Session.Models.Current);

        Assert.Equal(ExitCodes.Success, providers.Remove("p2"));
        Assert.DoesNotContain("错误：", error.ToString());
        Assert.NotEmpty(output.ToString());
    }

    [Fact]
    public async Task Removing_a_provider_in_use_is_rejected()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (ProviderCommands providers, _, _, StringWriter error) = Build(environment);

        int code = providers.Remove("test");

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("错误：", error.ToString());
    }

    [Fact]
    public async Task Provider_credential_falls_back_to_environment()
    {
        string? previous = Environment.GetEnvironmentVariable(ProviderCommands.CredentialEnvironmentVariable);
        Environment.SetEnvironmentVariable(ProviderCommands.CredentialEnvironmentVariable, "env-key");
        try
        {
            await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
            (ProviderCommands providers, _, _, _) = Build(environment);

            Assert.Equal(ExitCodes.Success, providers.Add("p3", "https://example.invalid/v3", null));
            Assert.Equal("env-key", environment.Session.Catalog.Snapshot().Providers
                .Single(provider => provider.ProviderName.Value == "p3").ApiKey.Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProviderCommands.CredentialEnvironmentVariable, previous);
        }
    }

    [Fact]
    public async Task Missing_provider_credential_is_rejected()
    {
        string? previous = Environment.GetEnvironmentVariable(ProviderCommands.CredentialEnvironmentVariable);
        Environment.SetEnvironmentVariable(ProviderCommands.CredentialEnvironmentVariable, null);
        try
        {
            await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
            (ProviderCommands providers, _, _, StringWriter error) = Build(environment);

            int code = providers.Add("p4", "https://example.invalid/v4", null);

            Assert.Equal(ExitCodes.Failure, code);
            Assert.Contains("Cli.CredentialMissing", error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProviderCommands.CredentialEnvironmentVariable, previous);
        }
    }

    private static (ProviderCommands Providers, ModelCommands Models, StringWriter Output, StringWriter Error) Build(
        CliEnvironment environment)
    {
        StringWriter output = new();
        StringWriter error = new();
        CliOutput cli = new(output, error);

        return (new ProviderCommands(environment.Session, cli), new ModelCommands(environment.Session, cli), output, error);
    }
}