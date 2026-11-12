using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control;

/// <summary>流程实现的身份：存档种类、展示名与流程归属。</summary>
public interface IFlowFramework
{
    /// <summary>流程记录种类。</summary>
    string Kind { get; }

    /// <summary>流程展示名。</summary>
    string Name { get; }

    /// <summary>是否负责该流程名。</summary>
    bool Handles(FlowName name);

    /// <summary>是否负责该存档流程记录。恢复按此选择实现，同一种类可以有多个实现。</summary>
    bool Handles(FlowRecord record);
}