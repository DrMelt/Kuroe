using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>流程实现的身份判定与展示名。</summary>
public interface IFlowIdentity
{
    /// <summary>流程展示名。</summary>
    string DisplayName { get; }

    /// <summary>是否负责该流程名的提交。</summary>
    bool Handles(FlowName name);

    /// <summary>是否负责该身份的恢复。</summary>
    bool Handles(FlowIdentity identity);
}