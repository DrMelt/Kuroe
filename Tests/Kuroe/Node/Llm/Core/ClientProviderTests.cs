using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Node.Llm.Sessions;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>模型客户端的解析与缓存：接入信息可解析为单回合请求，未注册的模型给出接入错误，
/// 接入信息未变时复用客户端，变化时重建。</summary>
public sealed class ClientProviderTests
{
    [Fact]
    public void Resolve_returns_a_turn_client_for_a_registered_model()
    {
        using ClientProvider provider = new(new Access());

        ErrorOr<ITurnClient> client = provider.Resolve(ModelName.Create("fake").Value);

        Assert.False(client.IsError);
        Assert.NotNull(client.Value);
    }

    [Fact]
    public void Resolve_reports_the_access_error()
    {
        using ClientProvider provider = new(new Access());

        ErrorOr<ITurnClient> client = provider.Resolve(ModelName.Create("missing").Value);

        Assert.True(client.IsError);
        Assert.Equal("Model.Missing", client.FirstError.Code);
    }

    [Fact]
    public void Resolve_reuses_the_client_for_an_unchanged_connection()
    {
        using ClientProvider provider = new(new Access());
        ModelName model = ModelName.Create("fake").Value;

        ChatTurnClient first = Assert.IsType<ChatTurnClient>(provider.Resolve(model).Value);
        ChatTurnClient second = Assert.IsType<ChatTurnClient>(provider.Resolve(model).Value);

        Assert.Same(first.Client, second.Client);
    }

    [Fact]
    public void Resolve_rebuilds_the_client_when_the_connection_changes()
    {
        Access access = new();
        using ClientProvider provider = new(access);
        ModelName model = ModelName.Create("fake").Value;

        ChatTurnClient first = Assert.IsType<ChatTurnClient>(provider.Resolve(model).Value);
        access.Key = "other";
        ChatTurnClient second = Assert.IsType<ChatTurnClient>(provider.Resolve(model).Value);

        Assert.NotSame(first.Client, second.Client);
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
