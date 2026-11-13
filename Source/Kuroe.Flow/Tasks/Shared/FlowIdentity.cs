namespace Kuroe.Flow.Tasks.Shared;

/// <summary>流程身份：流程名、流程种类与实现标识。提交解析只比对流程名，恢复解析比对种类与实现标识。</summary>
public sealed record FlowIdentity(FlowName Name, string Kind, string Implementation);