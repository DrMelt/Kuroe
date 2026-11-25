using System.Collections.Concurrent;
using Kuroe.TestSupport;

namespace Kuroe.Console.Tests.Persistence;

/// <summary>Console 宿主的任务持久化验收：共享的内存文件访问里跨会话重载任务。</summary>
public sealed class ConsoleTaskPersistenceAcceptanceTests : TaskPersistenceAcceptanceTests
{
    private readonly ConcurrentDictionary<string, (InMemoryWorkspaceFiles Files, InMemoryTaskLeaseStores Leases)> _workspaces = new();

    protected override string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        _workspaces[root] = (new InMemoryWorkspaceFiles(), new InMemoryTaskLeaseStores());

        return root;
    }

    protected override void DeleteRoot(string root) => _workspaces.TryRemove(root, out _);

    protected override Task<ITaskPersistenceSession> CreateSessionAsync(string root)
    {
        (InMemoryWorkspaceFiles files, InMemoryTaskLeaseStores leases) = _workspaces[root];

        return ConsoleTaskPersistenceSession.CreateAsync(root, files, leases);
    }
}
