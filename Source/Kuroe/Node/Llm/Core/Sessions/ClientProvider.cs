using ApiHub.ChatClient;
using ApiHub.Shared.Models;
using ErrorOr;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>按模型名缓存模型客户端，接入信息变化时重建。多个模型并发时各自持有客户端，
/// 不会重建别人正在用的那一个。重建与退出时释放旧客户端。</summary>
sealed class ClientProvider(IModelAccess access) : ITurnClientSource, IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ModelConnection Connection, IChatClient Client)> _clients = [];

    public ErrorOr<ITurnClient> Resolve(ModelName model)
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
                    return new ChatTurnClient(cached.Client);
                }

                cached.Client.Dispose();
            }

            IChatClient client = connection.CreateChatClient();
            _clients[model.Value] = (connection, client);

            return new ChatTurnClient(client);
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

