using ApiHub.ChatClient;
using ApiHub.Shared.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kuroe.Executions.Sessions;

/// <summary>按模型名缓存会话客户端，接入信息变化时重建。多个模型并发时各自持有客户端，
/// 不会重建别人正在用的那一个。客户端不负责底层客户端的释放，这里保留原客户端在重建或退出时释放。
/// 错误由调用方解析连接时给出。</summary>
sealed class ClientProvider(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ModelConnection Connection, ChatClientAgent Agent, IChatClient Client)> _clients = [];

    /// <summary>该模型的会话客户端，接入信息与上次不同则重建。缓存未命中时没有旧客户端可释放。</summary>
    public ChatClientAgent GetAgent(ModelName model, ModelConnection connection)
    {
        lock (_gate)
        {
            if (_clients.TryGetValue(model.Value, out (ModelConnection Connection, ChatClientAgent Agent, IChatClient Client) cached) && cached.Connection == connection)
            {
                return cached.Agent;
            }

            if (cached.Agent is not null)
            {
                cached.Client.Dispose();
            }

            IChatClient client = connection.CreateChatClient();
            ChatClientAgent agent = client.AsAIAgent(new ChatClientAgentOptions { Name = model.Value }, loggerFactory);
            _clients[model.Value] = (connection, agent, client);

            return agent;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach ((ModelConnection _, ChatClientAgent _, IChatClient client) in _clients.Values)
            {
                client.Dispose();
            }

            _clients.Clear();
        }
    }
}

