namespace Kuroe.Agent;

/// <summary>agent 在流程中承担的角色。规划、实施、检查共用同一执行类型，差别只在角色与所处步骤。</summary>
public enum RunRole
{
    /// <summary>拆分目标，交回可独立实施的条目列表。</summary>
    Plan,

    /// <summary>实施一个条目或整个步骤。</summary>
    Implement,

    /// <summary>检查实施产出，交回结论。</summary>
    Check,
}

/// <summary>角色的展示名。</summary>
public static class RunRoles
{
    public static string Label(this RunRole role) => role switch
    {
        RunRole.Plan => "规划",
        RunRole.Implement => "实施",
        RunRole.Check => "检查",
        _ => role.ToString(),
    };
}
