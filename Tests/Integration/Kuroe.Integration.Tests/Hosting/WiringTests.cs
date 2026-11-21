using ErrorOr;
using Kuroe.Assistant;
using Kuroe.Composition;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Runner.Llm.Sessions;
using Kuroe.Runner.Llm.Tools;
using Kuroe.Composition.Catalogs;
using Kuroe.Composition.Configuration;
using Kuroe.Composition.Configuration.Files;
using Kuroe.Tool;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.GraphFlow;
using Kuroe.Flow.Library;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>不接模型地解析整张服务图：推进、执行与会话装配都建得起来。
/// 其余测试用替身运行器接管 Llm 节点，内建节点使用各自运行器。</summary>
public sealed class WiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact(DisplayName = "真实执行运行器可解析")]
    public void Real_execution_runner_resolves()
    {
        using ServiceProvider provider = Services();

        Assert.NotEmpty(provider.GetServices<IExecutionRunnerFactory>());
        Assert.NotNull(provider.GetRequiredService<Kernel.Execution.ExecutionRuntime>());
        Assert.NotNull(provider.GetRequiredService<SessionFactory>());
    }

    [Fact(DisplayName = "配置的命令工具加入工具面")]
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

    [Fact(DisplayName = "命令工具重复内置路径时启动失败")]
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

        ErrorOr<KuroeComposition> startup = services.AddKuroeComposition(KuroePaths.At(_root), TestEnvironment.Options);

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList, error => error.Description.Contains("重复", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "命令工具与内置调用名冲突时启动失败")]
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

        ErrorOr<KuroeComposition> startup = services.AddKuroeComposition(KuroePaths.At(_root), TestEnvironment.Options);

        Assert.True(startup.IsError);
        Assert.Contains(startup.ErrorsOrEmptyList,
            error => error.Description.Contains("模型调用名", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "每个注册工具都有模型接口接受的名称")]
    public void Every_registered_tool_has_a_name_the_model_interface_accepts()
    {
        using ServiceProvider provider = Services();

        IReadOnlyList<string> names = [.. provider.GetRequiredService<ToolCollection>()
            .Definitions
            .Select(definition => definition.Path.CallName.Value)];

        Assert.Contains("file_read_ReadFile", names);
        Assert.All(names, name => Assert.Matches("^[a-zA-Z0-9_-]+$", name));
    }

    [Fact(DisplayName = "内置 Git 工具加入工具面")]
    public void Builtin_git_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        IReadOnlyList<string> paths =
            [.. provider.GetRequiredService<ToolCollection>()
                .Definitions.Select(definition => definition.Path.Value)];
        Assert.Contains("git/read/Status", paths);
        Assert.Contains("git/write/Restore", paths);
        Assert.Contains("git/branch/Switch", paths);
    }

    [Fact(DisplayName = "内置 dotnet 工具加入工具面")]
    public void Builtin_dotnet_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        IReadOnlyList<string> paths =
            [.. provider.GetRequiredService<ToolCollection>()
                .Definitions.Select(definition => definition.Path.Value)];

        Assert.Contains("dotnet/read/Info", paths);
        Assert.Contains("dotnet/write/Build", paths);
    }

    [Fact(DisplayName = "宿主需要的每个服务都可解析")]
    public void Every_service_the_host_needs_resolves()
    {
        using ServiceProvider provider = Services();

        Assert.NotNull(provider.GetRequiredService<SettingsProvider>());
        Assert.NotNull(provider.GetRequiredService<CatalogService>());
        Assert.NotNull(provider.GetRequiredService<FlowService>());
        Assert.NotNull(provider.GetRequiredService<ModelService>());
        Assert.NotNull(provider.GetRequiredService<ToolCollection>());
        Assert.NotNull(provider.GetRequiredService<TaskRegistry>());
        Assert.NotNull(provider.GetRequiredService<Kernel.Execution.ExecutionDispatcher>());
        Assert.NotNull(provider.GetRequiredService<IExecutionRuntime>());
        Assert.NotNull(provider.GetRequiredService<TaskService>());
        Assert.NotNull(provider.GetRequiredService<ITaskControl>());
        Assert.NotEmpty(provider.GetServices<IToolSource>());
    }

    private ServiceProvider Services()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeComposition> composition = services.AddKuroeComposition(KuroePaths.At(_root), TestEnvironment.Options);
        Assert.False(composition.IsError, composition.ErrorsText());

        ErrorOr<Success> graph = services.AddKuroeGraphFlows(composition.Value);
        Assert.False(graph.IsError, graph.ErrorsText());

        ErrorOr<Success> assistant = services.AddKuroeAssistant(composition.Value, Path.Combine(_root, "source"));
        Assert.False(assistant.IsError, assistant.ErrorsText());

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
