namespace Kuroe.Tool;

/// <summary>工具源里的一条：函数定义与按工具绑定创建载体的工厂。</summary>
/// <param name="Definition">函数的定义。</param>
/// <param name="Create">按工具绑定创建载体。</param>
public sealed record ToolEntry(ToolDefinition Definition, Func<ToolBinding, ITool> Create)
{
    /// <summary>用不依赖执行绑定的载体工厂建立条目。</summary>
    public static ToolEntry Of(ToolDefinition definition, Func<ITool> create) =>
        new(definition, _ => create());
}