using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.Maf.Compilation;
using Kuroe.Maf.Compilation.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Maf.Control;
using Kuroe.Maf.Persistence;
using Kuroe.Kernel.Tasks.Records.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Maf;

/// <summary>MAF 流程的装配入口。宿主在核心装配之上调用，流程实现与存档编解码一并注册。</summary>
public static class MafServiceCollectionExtensions
{
    /// <summary>按给定流程定义注册 MAF 流程实现、流程记录编解码与流程模板目录登记。</summary>
    public static IServiceCollection AddKuroeMafFlows(
        this IServiceCollection services,
        IFlowTemplateRegistrar registrar,
        params MafFlowDefinition[] flows) =>
        services.AddKuroeMafFlows(registrar, new MafFlowCatalog(flows));

    /// <summary>按流程描述装配 MAF 流程，装配入口与现成定义使用同一套注册。</summary>
    public static ErrorOr<Success> AddKuroeMafFlows(
        this IServiceCollection services,
        IFlowTemplateRegistrar registrar,
        IFlowDescriptionCompiler compiler,
        IMafNodeExecutions executions,
        FlowDescriptionSet document)
    {
        ErrorOr<MafDocumentFlowSource> source = MafDocumentFlowSource.Create(
            new MafFlowAssembler(compiler, executions),
            document);
        if (source.IsError)
        {
            return source.ErrorsOrEmptyList;
        }

        services.AddKuroeMafFlows(registrar, source.Value);

        return Result.Success;
    }

    /// <summary>按给定流程定义来源注册 MAF 流程实现、流程记录编解码与流程模板目录登记。</summary>
    public static IServiceCollection AddKuroeMafFlows(
        this IServiceCollection services,
        IFlowTemplateRegistrar registrar,
        IMafFlowSource flows)
    {
        MafFlowTemplates templates = new(flows);
        registrar.Register(templates);
        services.AddSingleton(flows);
        services.AddSingleton<IFlowTemplateSource>(templates);
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