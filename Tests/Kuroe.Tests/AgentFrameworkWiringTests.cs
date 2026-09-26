using ApiHub.ChatClient;
using ApiHub.Shared.Models;
using Kuroe;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace Kuroe.Tests;

/// <summary>Agent Framework 与 ApiHub 客户端的装配契约：接入信息可包装为 ChatClientAgent，工具在每轮选项里传入。</summary>
public sealed class AgentFrameworkWiringTests
{
    [Fact]
    public void ApiHub_client_wraps_into_chat_client_agent()
    {
        ModelConnection connection = Connection();

        ChatClientAgent agent = connection.CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions { Name = "fake" });

        Assert.Equal("fake", agent.Name);
        Assert.NotNull(agent.ChatClient);
        Assert.False(string.IsNullOrWhiteSpace(agent.Id));
    }

    [Fact]
    public void Run_options_carry_tools_and_sampling()
    {
        ChatClientAgent agent = Connection().CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions());

        var options = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0.7f,
            MaxOutputTokens = 256,
            Tools = [AIFunctionFactory.Create(() => "ok")],
        });

        Assert.NotNull(options.ChatOptions);
        Assert.Equal(0.7f, options.ChatOptions.Temperature);
        Assert.NotNull(options.ChatOptions.Tools);
        Assert.Single(options.ChatOptions.Tools);
    }

    /// <summary>未缓存的模型首次解析不触碰不存在的旧客户端，重复解析命中同一实例。</summary>
    [Fact]
    public void GetAgent_creates_for_missing_cache_and_reuses_cached()
    {
        Type providerType = typeof(ServiceCollectionExtensions).Assembly
            .GetType("Kuroe.Agent.Sessions.AgentProvider", throwOnError: true)!;
        object provider = Activator.CreateInstance(providerType, [NullLoggerFactory.Instance])!;
        MethodInfo getAgent = providerType.GetMethod("GetAgent", BindingFlags.Instance | BindingFlags.Public)!;

        ModelConnection connection = Connection();

        ChatClientAgent first = Assert.IsType<ChatClientAgent>(getAgent.Invoke(provider, ["fake", connection]));
        ChatClientAgent second = Assert.IsType<ChatClientAgent>(getAgent.Invoke(provider, ["fake", connection]));

        Assert.Same(first, second);

        ((IDisposable)provider).Dispose();
    }

    private static ModelConnection Connection() => ModelConnection.Create(
        ModelDefinition.Create(ModelName.Create("fake").Value, ProviderName.Create("test").Value),
        ProviderDefinition.Create(
            ProviderName.Create("test").Value,
            ProviderEndpoint.Create("https://example.invalid/v1").Value,
            ApiKey.Create("key").Value));
}