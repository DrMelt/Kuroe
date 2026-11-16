using Kuroe.Assistant.Dialogue.Flow;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Control.Runtime;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Input.Shared;
using Kuroe.Pending;
using Kuroe.Tasks.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话宿主的构造：自有任务域、执行网关、流程提供者与任务动作。</summary>
internal static class DialogueHostFactory
{
    /// <summary>按容器建立常驻对话宿主。对话任务域不注册为 TaskRegistry，避免覆盖用户任务域。</summary>
    internal static DialogueHost Create(IServiceProvider services)
    {
        TaskRegistry registry = new(
            services.GetRequiredService<ExecutionIndex>(),
            services.GetRequiredService<IInputDelivery>(),
            pauseOnRestore: false);
        DialogueRound round = new(
            registry,
            new PendingService(registry));

        ModelService models = services.GetRequiredService<ModelService>();

        TaskExecutionGateway gateway = new(
            services.GetRequiredService<ExecutionIndex>(),
            services.GetRequiredService<IExecutionRuntime>());

        // 对话流程由代码定义，不进入图流程库与宿主开始输入表
        FlowProvider provider = new(
            new DialogueFlowIdentity(),
            new DialogueFlowSubmission(
                models,
                services.GetRequiredService<SettingsProvider>(),
                services.GetRequiredService<KuroePaths>(),
                gateway),
            null,
            new DialogueFlowControlFactory(
                gateway,
                services.GetRequiredService<IInputChannel>()),
            new DialogueFlowPresentation(),
            new DialogueFlowRecordSource());

        // 对话任务自有任务域，任务键不占用用户任务域的序列
        GuidTaskIdSource ids = new();
        TaskActions actions = new(
            registry,
            services.GetRequiredService<IExecutionRuntime>(),
            services.GetRequiredService<IFlowRuntime>(),
            new FlowProviderSource([provider]),
            ids);

        return new DialogueHost(actions, provider, round, models, registry);
    }
}