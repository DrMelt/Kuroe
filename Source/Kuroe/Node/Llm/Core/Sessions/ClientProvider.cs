using ApiHub.ChatClient;
using ApiHub.Shared.Models;
using ErrorOr;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>一次模型客户端解析的结果：接入成功时给出客户端。</summary>
internal sealed record ResolvedClient(IChatClient Client);

/// <summary>按模型名缓存模型客户端，接入信息变化时重建。多个模型并发时各自持有客户端，
/// 不会重建别人正在用的那一个。重建与退出时释放旧客户端。</summary>
internal sealed class ClientProvider(IModelAccess access) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ModelConnection Connection, IChatClient Client)> _clients = [];

    /// <summary>按模型取基础客户端，接入信息变化时重建。</summary>
    internal ErrorOr<ResolvedClient> Resolve(ModelName model)
    {
        ErrorOr<ModelConnection> resolved = access.Connect(model);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        ModelConnection connection = resolved.Value;
        lock (_gate)
        {
            if (_clients.TryGetValue(model.Value, out (ModelConnection Connection, IChatClient Client) cached))
            {
                if (cached.Connection == connection)
                {
                    return new ResolvedClient(cached.Client);
                }

                cached.Client.Dispose();
            }

            IChatClient client = connection.CreateChatClient();
            _clients[model.Value] = (connection, client);

            return new ResolvedClient(client);
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

