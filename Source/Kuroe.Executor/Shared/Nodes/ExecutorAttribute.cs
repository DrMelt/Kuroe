namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器条目提供给信息查询的一条属性。</summary>
/// <param name="Name">属性名。</param>
/// <param name="Value">属性值。</param>
public sealed record ExecutorAttribute(string Name, string Value);