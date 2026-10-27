namespace Kuroe.Flow.Shared.Tools;

/// <summary>工具的装载期契约：工具路径与路径变量名。流程编译按它校验节点映射。</summary>
/// <param name="Path">工具在工具层级里的完整路径。</param>
/// <param name="Variables">工具声明的路径变量名。</param>
public sealed record ToolContract(string Path, IReadOnlyList<string> Variables);