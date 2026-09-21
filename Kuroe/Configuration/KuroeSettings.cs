using ErrorOr;
using Kuroe.Agent;
using Microsoft.Extensions.Configuration;

namespace Kuroe.Configuration;

/// <summary>全部配置节的绑定结果。</summary>
public sealed record KuroeSettings
{
    public required AgentSettings Agent { get; init; }

    public static ErrorOr<KuroeSettings> From(IConfiguration configuration)
    {
        ErrorOr<AgentSettings> agent = AgentSettings.From(configuration.GetSection(AgentSettings.SectionName));

        return agent.IsError ? agent.ErrorsOrEmptyList : new KuroeSettings { Agent = agent.Value };
    }
}