using ErrorOr;
using Kuroe.Assistant;
using Kuroe.Configuration.Files;
using Kuroe.Node.Llm.Tools;
using Kuroe.Shared;
using Kuroe.Tool;
using Kuroe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kuroe.Tests;

/// <summary>内置工具面的装配：各载体在核心装配里注册后进入工具集。</summary>
public sealed class ToolFaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void File_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        ToolCollection tools = provider.GetRequiredService<ToolCollection>();

        Assert.Contains(new ToolName("ReadFile"), tools.Names);
        Assert.Contains(new ToolName("ListDirectory"), tools.Names);
        Assert.Contains(new ToolName("ReadFiles"), tools.Names);
        Assert.Contains(new ToolName("WriteFile"), tools.Names);
        Assert.Contains(new ToolName("PatchFile"), tools.Names);
        Assert.Contains(new ToolName("GetFileInfo"), tools.Names);
    }

    [Fact]
    public void Source_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        ToolCollection tools = provider.GetRequiredService<ToolCollection>();

        Assert.Contains(new ToolName("ListSourceDirectory"), tools.Names);
        Assert.Contains(new ToolName("ReadSourceFile"), tools.Names);
    }

    [Fact]
    public void Information_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        ToolCollection tools = provider.GetRequiredService<ToolCollection>();

        Assert.Contains(new ToolName("ListTasks"), tools.Names);
        Assert.Contains(new ToolName("GetCatalog"), tools.Names);
        Assert.Contains(new ToolName("ListFlows"), tools.Names);
        Assert.Contains(new ToolName("GetConfig"), tools.Names);
        Assert.Contains(new ToolName("ListTools"), tools.Names);
    }

    /// <summary>在临时工作目录里装配整套服务，用于工具面装配断言。</summary>
    private ServiceProvider Services()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeComposition> composition = services.AddKuroeCore(KuroePaths.At(_root), TestEnvironment.Options);
        Assert.False(composition.IsError);

        ErrorOr<Success> assistant = services.AddKuroeAssistant(composition.Value, Path.Combine(_root, "source"));
        Assert.False(assistant.IsError);

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