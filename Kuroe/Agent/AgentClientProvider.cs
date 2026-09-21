using ApiHub.ChatClient;
using ApiHub.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Kuroe.Agent;

/// <summary>按接入信息缓存带中间件的模型客户端，信息变化时重建。错误由调用方解析连接时给出。</summary>
public sealed class AgentClientProvider(ILoggerFactory loggerFactory) : IDisposable
{
    private readonly Lock _gate = new();
    private IChatClient? _client;
    private ModelConnection? _connection;

    /// <summary>该接入信息对应的客户端，与上一次不同时重建。</summary>
    public IChatClient GetClient(ModelConnection connection)
    {
        lock (_gate)
        {
            if (_client is null || connection != _connection)
            {
                _client?.Dispose();
                _client = connection.CreateChatClient()
                    .AsBuilder()
                    .UseFunctionInvocation(loggerFactory)
                    .Build();
                _connection = connection;
            }

            return _client;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _client?.Dispose();
            _client = null;
            _connection = null;
        }
    }
}
