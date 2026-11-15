using Kuroe.Tasks.Records.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.CodeFlow.Persistence;

/// <summary>代码流程记录装配入口：注册代码流程记录的存档编解码。</summary>
public static class CodeFlowServiceCollectionExtensions
{
    /// <summary>注册代码流程记录编解码。使用代码流程记录的宿主调用一次。</summary>
    public static IServiceCollection AddKuroeCodeFlowRecords(this IServiceCollection services)
    {
        services.AddSingleton<IFlowRecordCodec, CodeFlowRecordCodec>();

        return services;
    }
}