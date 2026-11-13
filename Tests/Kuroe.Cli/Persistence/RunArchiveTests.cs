using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Persistence.Files;
using Kuroe.Flow.Persistence.Json;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Cli.Tests.Persistence;

/// <summary>run 写存档并占用租约，退出时收口存档并释放租约。</summary>
public sealed class RunArchiveTests
{
    [Fact]
    public async Task Done_run_writes_the_archive_and_releases_the_lease_on_shutdown()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        RunCommand command = new(environment.Session, new CliOutput(new StringWriter(), new StringWriter()));

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

        Assert.Equal(ExitCodes.Success, code);
        TaskId id = Assert.Single(environment.Session.Registry.Snapshots()).Id;
        string records = KuroePaths.At(environment.Root).RecordsDirectory;
        string recordFile = Path.Combine(records, $"{id.Value}.json");
        string lockFile = Path.Combine(records, $"{id.Value}.lock");
        Assert.True(File.Exists(recordFile));
        Assert.True(File.Exists(lockFile));

        using FileTaskLeaseStore probe = new(records);
        Assert.True(probe.TryAcquire(id).IsError);

        await environment.Session.Tasks.ShutdownAsync();

        Assert.False(probe.TryAcquire(id).IsError);
        TaskRecord record = Kuroe.Flow.TestSupport.TaskRecordCodecs.Codec().Read(await File.ReadAllTextAsync(recordFile, TestContext.Current.CancellationToken)).ThrowIfError();
        Assert.Equal(id, record.Identity.Id);
        Assert.True(record.Completed);
    }

    [Fact]
    public async Task Blocked_run_archives_the_unfinished_task()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Blocked);
        RunCommand command = new(environment.Session, new CliOutput(new StringWriter(), new StringWriter()));

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

        Assert.Equal(ExitCodes.Blocked, code);
        TaskId id = Assert.Single(environment.Session.Registry.Snapshots()).Id;
        await environment.Session.Tasks.ShutdownAsync();
        string recordFile = Path.Combine(KuroePaths.At(environment.Root).RecordsDirectory, $"{id.Value}.json");
        TaskRecord record = Kuroe.Flow.TestSupport.TaskRecordCodecs.Codec().Read(await File.ReadAllTextAsync(recordFile, TestContext.Current.CancellationToken)).ThrowIfError();

        Assert.False(record.Completed);
        Assert.Contains(State(record).Nodes, node => node is ExecutableNodeRecord { Block: not null });
    }

    private static GraphFlowStateRecord State(TaskRecord record) =>
        Assert.IsType<GraphFlowStateRecord>(record.Flow.State);
}