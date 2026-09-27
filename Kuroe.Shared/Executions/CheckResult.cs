namespace Kuroe.Shared.Executions;

/// <summary>一次检查活动的结论：第几轮检查、通过与否、意见、交回者与检查执行节点。
/// 轮次即检查 run 在该执行节点的执行次数，交回时定下；退回返工不新增结论，既有结论保留供下一轮实施阅读。</summary>
public sealed record CheckResult(int Round, bool Passed, string Findings, RunId Origin, string NodeName);