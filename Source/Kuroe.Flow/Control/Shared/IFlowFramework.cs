using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Control;

/// <summary>流程实现的身份：存档种类、展示名与流程名归属。</summary>
public interface IFlowFramework
{
    /// <summary>流程记录种类。</summary>
    string Kind { get; }

    /// <summary>流程展示名。</summary>
    string Name { get; }

    /// <summary>是否负责该流程名。</summary>
    bool Handles(FlowName name);
}