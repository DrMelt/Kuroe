using ApiHub.ChatClient;
using ApiHub.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kuroe.Agent.Sessions;

/// <summary>按模型名缓存带中间件的客户端，接入信息变化时重建。多个模型并发时各自持有客户端，
/// 不会重建别人正在用的那一个。错误由调用方解析连接时给出。</summary>
sealed class AgentClientProvider(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ModelConnection Connection, IChatClient Client)> _clients = [];

    /// <summary>该模型的客户端，接入信息与上次不同则重建。</summary>
    public IChatClient GetClient(string model, ModelConnection connection)
    {
        lock (_gate)
        {
            if (!_clients.TryGetValue(model, out (ModelConnection Connection, IChatClient Client) cached)
                || cached.Connection != connection)
            {
                cached.Client?.Dispose();
                IChatClient client = connection.CreateChatClient()
                    .AsBuilder()
                    .UseFunctionInvocation(loggerFactory)
                    .Build();
                _clients[model] = (connection, client);

                return client;
            }

            return cached.Client;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach ((ModelConnection _, IChatClient client) in _clients.Values)
            {
                client.Dispose();
            }

            _clients.Clear();
        }
    }
}

