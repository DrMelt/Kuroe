using Kuroe.Kernel.Control.Runtime;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Tasks.Records.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>MAF 流程的装配入口：流程实现与存档编解码一并注册。</summary>
public sealed class MafRegistrationTests
{
    [Fact(DisplayName = "注册暴露一个提供方与一个编解码器")]
    public void Registration_exposes_one_provider_and_one_codec()
    {
        ServiceCollection services = new();
        FlowTemplateCatalog templates = new([]);
        services.AddSingleton<ITaskExecutionGateway>(new UnusedGateway());
        services.AddKuroeMafFlows(templates, TestFlow.Definition());

        using ServiceProvider provider = services.BuildServiceProvider();

        FlowProvider registered = Assert.Single(provider.GetServices<FlowProvider>());
        Assert.Single(provider.GetServices<IFlowRecordCodec>());
        Assert.IsType<MafFlowIdentity>(registered.Identity);
    }
}