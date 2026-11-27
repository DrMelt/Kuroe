using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>流程实现的身份判定与展示名。</summary>
public interface IFlowIdentity
{
    /// <summary>流程展示名。</summary>
    string DisplayName { get; }

    /// <summary>流程实现标识，用于同名流程的实现选择。</summary>
    string Implementation { get; }

    /// <summary>是否负责该流程名的提交。</summary>
    bool Handles(FlowName name);

    /// <summary>是否负责该身份的恢复。</summary>
    bool Handles(FlowIdentity identity);
}