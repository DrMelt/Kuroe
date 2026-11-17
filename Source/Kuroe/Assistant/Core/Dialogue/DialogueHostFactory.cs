using Kuroe.Assistant.Dialogue.Flow;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Control.Runtime;
using Kuroe.Control.Shared;
using Kuroe.Input.Shared;
using Kuroe.Tasks;
using Kuroe.Tasks.Persistence.Serialization.Shared;
using Kuroe.Tasks.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Kuroe.Execution.Shared.Conversation;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话宿主的构造：独立的对话任务域、存档目录、租约与恢复服务。</summary>
internal static class DialogueHostFactory
{
    /// <summary>按容器建立会话宿主。对话任务域不注册为用户任务域，存档与租约使用助手目录。</summary>
    internal static DialogueHost Create(IServiceProvider services, bool persistTasks)
    {
        TaskRegistry registry = new(
            services.GetRequiredService<ExecutionIndex>(),
            services.GetRequiredService<IInputDelivery>(),
            pauseOnRestore: false);
        ModelService models = services.GetRequiredService<ModelService>();
        SettingsProvider settings = services.GetRequiredService<SettingsProvider>();
        KuroePaths paths = services.GetRequiredService<KuroePaths>();
        ITaskExecutionGateway gateway = services.GetRequiredService<ITaskExecutionGateway>();

        // 对话流程由代码定义，不进入图流程库与宿主开始输入表
        FlowProvider provider = new(
            new DialogueFlowIdentity(),
            new DialogueFlowSubmission(models, settings, paths, gateway),
            null,
            new DialogueFlowControlFactory(
                gateway,
                services.GetRequiredService<IInputChannel>()),
            new DialogueFlowPresentation(),
            new DialogueFlowRecordSource());
        FlowProviderSource providers = new([provider]);

        TaskDomain domain = TaskDomainComposition.Create(
            registry,
            services.GetRequiredService<IExecutionRuntime>(),
            services.GetRequiredService<IFlowRuntime>(),
            providers,
            new GuidTaskIdSource(),
            persistTasks,
            persistTasks ? paths.AssistantRecordsDirectory : null,
            persistTasks ? services.GetRequiredService<ITaskRecordCodec>() : null);

        DialogueRound round = new(registry, domain.Pendings);
        DialogueTurns turns = new(registry, round);
        DialogueSessions sessions = new(domain.Actions, registry, models, settings, provider, turns);
        DialogueHistoryReader history = new(
            registry,
            services.GetRequiredService<IExecutionConversationReader>());

        return new DialogueHost(domain.Actions, sessions, turns, history, domain.Restore);
    }
}