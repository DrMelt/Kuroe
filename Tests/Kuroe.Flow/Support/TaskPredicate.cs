using Kuroe.Flow.Tasks.Presentation.Shared;

namespace Kuroe.Flow.TestSupport;

/// <summary>对任务快照的条件，观察方据此判定是否已到达目标状态。</summary>
public delegate bool TaskPredicate(TaskSnapshot snapshot);