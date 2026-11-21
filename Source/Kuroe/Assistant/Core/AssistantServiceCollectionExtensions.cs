using ErrorOr;
using Kuroe.Assistant.Dialogue;
using Kuroe.Composition;
using Kuroe.Assistant.Tools;
using Kuroe.CodeFlow.Persistence;
using Kuroe.Tool.Implementation.Source;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Assistant;

/// <summary>内置助手的装配入口。宿主在核心装配之上调用，工具去重与核心共用同一工具集。</summary>
public static class AssistantServiceCollectionExtensions
{
    /// <summary>装配对话任务域、对话宿主与助手专用工具。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="composition">核心装配的产物，助手工具并入其中的工具集。</param>
    /// <param name="sourceDirectory">随程序导出的源码与文档目录。</param>
    public static ErrorOr<Success> AddKuroeAssistant(
        this IServiceCollection services,
        KuroeComposition composition,
        string sourceDirectory)
    {
        services.AddKuroeCodeFlowRecords();
        ErrorOr<Success> tools = composition.AddToolSources(
        [
            new SourceToolSource(sourceDirectory, composition.Files),
            new ToolboxToolSource(DialogueMode.All, composition.ConfiguredTools),
        ]);
        if (tools.IsError)
        {
            return tools.ErrorsOrEmptyList;
        }

        services.AddSingleton(sp => DialogueHostFactory.Create(sp, composition.PersistTasks));

        return Result.Success;
    }
}