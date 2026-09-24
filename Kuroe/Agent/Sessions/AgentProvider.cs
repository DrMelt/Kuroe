using ApiHub.ChatClient;
using ApiHub.Shared.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kuroe.Agent.Sessions;

/// <summary>按模型名缓存 agent，接入信息变化时重建。多个模型并发时各自持有 agent，
/// 不会重建别人正在用的那一个。agent 不负责底层客户端的释放，这里保留原客户端在重建或退出时释放。
/// 错误由调用方解析连接时给出。</summary>
sealed class AgentProvider(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ModelConnection Connection, ChatClientAgent Agent, IChatClient Client)> _agents = [];

    /// <summary>该模型的 agent，接入信息与上次不同则重建。</summary>
    public ChatClientAgent GetAgent(string model, ModelConnection connection)
    {
        lock (_gate)
        {
            if (!_agents.TryGetValue(model, out (ModelConnection Connection, ChatClientAgent Agent, IChatClient Client) cached)
                || cached.Connection != connection)
            {
                cached.Client.Dispose();
                IChatClient client = connection.CreateChatClient();
                ChatClientAgent agent = client.AsAIAgent(new ChatClientAgentOptions { Name = model }, loggerFactory);
                _agents[model] = (connection, agent, client);

                return agent;
            }

            return cached.Agent;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach ((ModelConnection _, ChatClientAgent _, IChatClient client) in _agents.Values)
            {
                client.Dispose();
            }

            _agents.Clear();
        }
    }
}

