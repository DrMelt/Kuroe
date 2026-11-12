using ErrorOr;
using Kuroe.Assistant.Dialogue;
using Kuroe.Assistant.Flows;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;
using Kuroe.Tool.Source;
using Kuroe.Workflows.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Assistant;

/// <summary>内置助手的装配入口。宿主在核心装配之上调用，工具去重与核心共用同一工具集。</summary>
public static class AssistantServiceCollectionExtensions
{
    /// <summary>装配对话任务域、对话宿主与助手专用工具。导出目录供 source 工具组查阅。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="composition">核心装配的产物，助手工具并入其中的工具集。</param>
    /// <param name="sourceDirectory">随程序导出的源码与文档目录。</param>
    public static ErrorOr<Success> AddKuroeAssistant(
        this IServiceCollection services,
        KuroeComposition composition,
        string sourceDirectory)
    {
        ErrorOr<Success> tools = composition.AddToolSources(
        [
            new SourceToolSource(sourceDirectory, composition.Files),
            new ToolboxToolSource(DialogueFlow.Tools, composition.ConfiguredTools),
        ]);
        if (tools.IsError)
        {
            return tools.ErrorsOrEmptyList;
        }

        services.AddSingleton(sp =>
        {
            // 对话任务自有任务域，不注册为 TaskRegistry，避免覆盖用户任务域
            TaskRegistry registry = new(sp.GetRequiredService<ExecutionIndex>(), pauseOnRestore: false);
            IFlowRuntime engine = sp.GetRequiredService<IFlowRuntime>();
            DialogueRound round = new(registry, new PendingService(
                registry,
                engine,
                sp.GetRequiredService<IInputDelivery>()));

            IExecutionTargetResolver targets = sp.GetRequiredService<IExecutionTargetResolver>();
            ModelService models = sp.GetRequiredService<ModelService>();

            // 对话任务只有内置流程一张表单，不与用户流程的宿主输入表混用
            HostStartInputs hostInputs = new(
                new Dictionary<FlowName, IReadOnlyDictionary<PortName, string>>
                {
                    [DialogueFlow.Name] = DialogueFlow.HostInputs,
                },
                StartSources.Supported);

            KuroeTaskStart start = new(sp.GetRequiredService<KuroePaths>().WorkingDirectory, null, models);
            // 对话任务自有任务域，任务键不占用用户任务域的序列
            GuidTaskIdSource ids = new();

            return new DialogueHost(
                new TaskActions(
                    registry,
                    engine,
                    targets,
                    hostInputs,
                    start,
                    ids,
                    sp.GetRequiredService<Kuroe.Flow.Execution.ExecutionRestorerRegistry>()),
                round,
                models,
                sp.GetRequiredService<SettingsProvider>(),
                registry,
                sp.GetRequiredService<IFlowDescriptionCompiler>());
        });

        return Result.Success;
    }
}