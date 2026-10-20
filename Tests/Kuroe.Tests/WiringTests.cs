using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Tool;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Workflows.Flows;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kuroe.Tests;

/// <summary>不接模型地解析整张服务图：推进、执行与会话装配都建得起来。
/// 其余测试把 IExecutionRunner 换成假的，走不到真实执行链。</summary>
public sealed class WiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Real_execution_runner_resolves()
    {
        using ServiceProvider provider = Services();

        Assert.NotNull(provider.GetRequiredService<IExecutionRunner>());
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
    public void Command_tool_duplicating_builtin_path_fails_startup()
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

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root), Path.Combine(_root, "source"));

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList, error => error.Description.Contains("重复", StringComparison.Ordinal));
    }

    [Fact]
    public void Command_tool_colliding_with_a_builtin_call_name_fails_startup()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "tools.json"), """
            {
              "Tools": [
                { "Name": "ReadFile", "Group": "file_read", "Template": ["cmd", "/c", "echo", "x"] }
              ]
            }
            """);

        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root), Path.Combine(_root, "source"));

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList,
            error => error.Description.Contains("模型调用名", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_registered_tool_has_a_name_the_model_interface_accepts()
    {
        using ServiceProvider provider = Services();

        IReadOnlyList<string> names = [.. provider.GetServices<ITool>()
            .SelectMany(tool => tool.Functions)
            .Select(function => function.Definition.Path.CallName.Value)];

        Assert.Contains("file_read_ReadFile", names);
        Assert.All(names, name => Assert.Matches("^[a-zA-Z0-9_-]+$", name));
    }

    [Fact]
    public void Builtin_git_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        IReadOnlyList<string> paths =
            [.. provider.GetServices<ITool>().SelectMany(tool => tool.Functions).Select(function => function.Definition.Path.Value)];

        Assert.Contains("git/read/Status", paths);
        Assert.Contains("git/write/Restore", paths);
    }

    [Fact]
    public void Builtin_dotnet_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        IReadOnlyList<string> paths =
            [.. provider.GetServices<ITool>().SelectMany(tool => tool.Functions).Select(function => function.Definition.Path.Value)];

        Assert.Contains("dotnet/read/Info", paths);
        Assert.Contains("dotnet/write/Build", paths);
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
        Assert.NotNull(provider.GetRequiredService<PortSubmitter>());
        Assert.NotNull(provider.GetRequiredService<ExecutionDispatcher>());
        Assert.NotNull(provider.GetRequiredService<IExecutionContract>());
        Assert.NotNull(provider.GetRequiredService<TaskService>());
        Assert.NotEmpty(provider.GetServices<ITool>());
    }

    private ServiceProvider Services()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root), Path.Combine(_root, "source"));
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
