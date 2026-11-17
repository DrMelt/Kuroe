using Kuroe.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Maf.Control;
using Kuroe.Maf.Persistence;
using Kuroe.Tasks.Records.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Maf;

/// <summary>MAF 流程的装配入口。宿主在核心装配之上调用，流程实现与存档编解码一并注册。</summary>
public static class MafServiceCollectionExtensions
{
    /// <summary>按给定流程定义注册 MAF 流程实现与流程记录编解码。</summary>
    public static IServiceCollection AddKuroeMafFlows(
        this IServiceCollection services,
        params MafFlowDefinition[] flows) =>
        services.AddKuroeMafFlows(new MafFlowCatalog(flows));

    /// <summary>按给定流程定义来源注册 MAF 流程实现与流程记录编解码。</summary>
    public static IServiceCollection AddKuroeMafFlows(
        this IServiceCollection services,
        IMafFlowSource flows)
    {
        services.AddSingleton(flows);
        services.AddSingleton<IFlowTemplateSource>(new MafFlowTemplates(flows));
        services.AddSingleton(sp => new FlowProvider(
            new MafFlowIdentity(flows),
            new MafFlowSubmission(flows, sp.GetRequiredService<ITaskExecutionGateway>()),
            null,
            new MafFlowControlFactory(sp.GetRequiredService<ITaskExecutionGateway>()),
            new MafFlowPresentation(),
            new MafFlowRecordSource()));
        services.AddSingleton<IFlowRecordCodec, MafFlowRecordCodec>();

        return services;
    }
}