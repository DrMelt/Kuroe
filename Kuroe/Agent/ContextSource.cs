namespace Kuroe.Agent;

/// <summary>上下文中一条内容的出处，详情视图据此回跳上游。</summary>
public abstract record ContextSource
{
    /// <summary>一行可读出处文本。</summary>
    public abstract string Label { get; }

    /// <summary>可跳转到的 agent，出处不是 agent 产出时为空。</summary>
    public virtual RunId? FromRun => null;
}

/// <summary>任务的某轮前台对话。</summary>
public sealed record DialogueSource(TaskId Task, int Turn) : ContextSource
{
    public override string Label => $"{Task} 第 {Turn} 回合";
}

/// <summary>某个 agent 在某步骤上的产出。</summary>
public sealed record AgentSource(RunId Run, string StepName) : ContextSource
{
    public override string Label => $"{Run} · {StepName} 产出";

    public override RunId? FromRun => Run;
}

/// <summary>规划步骤交回的某个条目。</summary>
public sealed record ItemSource(RunId Plan, int Index, string Title) : ContextSource
{
    public override string Label => $"{Plan} · 规划条目 {Index + 1}：{Title}";

    public override RunId? FromRun => Plan;
}
