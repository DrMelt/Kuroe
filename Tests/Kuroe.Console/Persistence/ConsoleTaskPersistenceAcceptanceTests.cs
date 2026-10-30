namespace Kuroe.Console.Tests.Persistence;

/// <summary>Console 宿主的任务持久化验收。验收涉及跨进程重载，默认作为手动场景保留。</summary>
public sealed class ConsoleTaskPersistenceAcceptanceTests : TaskPersistenceAcceptanceTests
{
    protected override string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    protected override void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }

    protected override Task<ITaskPersistenceSession> CreateSessionAsync(string root) =>
        ConsoleTaskPersistenceSession.CreateAsync(root);
}
