namespace Kuroe.Shared.Agent;

/// <summary>一次检查活动的结论：第几轮检查、通过与否、意见、交回者与检查叶子。
/// 轮次在结论产生时定下；退回返工不新增轮次，结论保留供下一轮实施阅读。</summary>
public sealed record CheckResult(int Round, bool Passed, string Findings, RunId Origin, string NodeName);