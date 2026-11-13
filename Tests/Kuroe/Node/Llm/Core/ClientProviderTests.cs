using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Node.Llm.Sessions;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>模型客户端的解析与缓存：接入信息可解析为基础客户端，未注册的模型给出接入错误，
/// 接入信息未变时复用客户端，变化时重建。</summary>
public sealed class ClientProviderTests
{
    [Fact]
    public void Resolve_returns_a_client_for_a_registered_model()
    {
        using ClientProvider provider = new(new Access());

        ErrorOr<ResolvedClient> client = provider.Resolve(ModelName.Create("fake").Value);

        Assert.False(client.IsError);
        Assert.NotNull(client.Value.Client);
    }

    [Fact]
    public void Resolve_reports_the_access_error()
    {
        using ClientProvider provider = new(new Access());

        ErrorOr<ResolvedClient> client = provider.Resolve(ModelName.Create("missing").Value);

        Assert.True(client.IsError);
        Assert.Equal("Model.Missing", Assert.Single(client.Errors).Code);
    }

    [Fact]
    public void Resolve_reuses_the_client_for_an_unchanged_connection()
    {
        using ClientProvider provider = new(new Access());
        ModelName model = ModelName.Create("fake").Value;

        ErrorOr<ResolvedClient> first = provider.Resolve(model);
        ErrorOr<ResolvedClient> second = provider.Resolve(model);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Same(first.Value.Client, second.Value.Client);
    }

    [Fact]
    public void Resolve_rebuilds_the_client_when_the_connection_changes()
    {
        Access access = new();
        using ClientProvider provider = new(access);
        ModelName model = ModelName.Create("fake").Value;

        ErrorOr<ResolvedClient> first = provider.Resolve(model);
        access.Key = "other";
        ErrorOr<ResolvedClient> second = provider.Resolve(model);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.NotSame(first.Value.Client, second.Value.Client);
    }

    private sealed class Access : IModelAccess
    {
        public string Key { get; set; } = "key";

        public ErrorOr<ModelConnection> Connect(ModelName model) =>
            model.Value == "fake"
                ? ModelConnection.Create(
                    ModelDefinition.Create(
                        model,
                        ProviderName.Create("test").Value,
                        ProviderModelName.Create("fake").Value),
                    ProviderDefinition.Create(
                        ProviderName.Create("test").Value,
                        ProviderEndpoint.Create("https://example.invalid/v1").Value,
                        ApiKey.Create(Key).Value))
                : Error.Validation("Model.Missing", "模型不存在。");

        public ErrorOr<Success> Check(ModelName model) => Result.Success;
    }
}
