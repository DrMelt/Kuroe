using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Sessions;
using Kuroe.Executions.Tools;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Workflows.FlowAssembly;
using Kuroe.Workflows.TaskExecution.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kuroe.Tests;

/// <summary>不接模型地解析整张服务图：推进、执行与会话装配都建得起来。
/// 其余测试把 IRunExecutor 换成假的，走不到真实执行链。</summary>
public sealed class WiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Real_run_executor_resolves()
    {
        using ServiceProvider provider = Services();

        Assert.NotNull(provider.GetRequiredService<IRunExecutor>());
        Assert.NotNull(provider.GetRequiredService<SessionFactory>());
    }

    [Fact]
    public void Configured_command_tools_join_the_tool_face()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "tools.json"), """
            {
              "Tools": [
                { "Name": "RunDiff", "Template": ["git", "diff", "{path}"], "Parameters": [ { "Name": "path", "Required": true } ] }
              ]
            }
            """);

        using ServiceProvider provider = Services();

        ToolCollection tools = provider.GetRequiredService<ToolCollection>();
        Assert.Contains(new ToolName("RunDiff"), tools.Names);
        Assert.Contains(new ToolName("GetLocalTime"), tools.Names);
    }

    [Fact]
    public void Command_tool_duplicating_builtin_name_fails_startup()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "tools.json"), """
            {
              "Tools": [
                { "Name": "GetLocalTime", "Template": ["cmd", "/c", "echo", "x"] }
              ]
            }
            """);

        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root));

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList, error => error.Description.Contains("重名", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_service_the_host_needs_resolves()
    {
        using ServiceProvider provider = Services();

        Assert.NotNull(provider.GetRequiredService<SettingsProvider>());
        Assert.NotNull(provider.GetRequiredService<CatalogService>());
        Assert.NotNull(provider.GetRequiredService<FlowService>());
        Assert.NotNull(provider.GetRequiredService<ModelService>());
        Assert.NotNull(provider.GetRequiredService<ToolCollection>());
        Assert.NotNull(provider.GetRequiredService<TaskRegistry>());
        Assert.NotNull(provider.GetRequiredService<PlanSubmitter>());
        Assert.NotNull(provider.GetRequiredService<RunDispatcher>());
        Assert.NotNull(provider.GetRequiredService<TaskService>());
        Assert.NotEmpty(provider.GetServices<ITool>());
    }

    private ServiceProvider Services()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root));
        Assert.False(startup.IsError);

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}
