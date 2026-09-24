using Kuroe.Shared.Agent;

namespace Kuroe.Agent;

/// <summary>角色的展示名。</summary>
public static class RunRoles
{
    /// <summary>角色的展示名。</summary>
    public static string Label(this RunRole role) => role switch
    {
        RunRole.Plan => "规划",
        RunRole.Implement => "实施",
        RunRole.Check => "检查",
        _ => role.ToString(),
    };
}