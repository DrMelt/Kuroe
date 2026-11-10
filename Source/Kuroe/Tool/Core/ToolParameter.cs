namespace Kuroe.Tool;

/// <summary>函数的一个参数。真假参数在交给模型的 schema 里是布尔，其余是字符串；路径参数仍是字符串，调用前按路径解析与校验。</summary>
/// <param name="Name">参数名，调用体按它取实参。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Flag">该参数是布尔值，缺省为字符串；与 <paramref name="Path"/> 互斥。</param>
/// <param name="Required">模型是否必须给出，缺省为否。</param>
/// <param name="List">该参数是字符串列表，模型以数组或单个字符串给出；与 <paramref name="Flag"/> 互斥，可与 <paramref name="Path"/> 组合。</param>
/// <param name="Path">该参数是路径，调用前按 <paramref name="PathAccess"/> 与工具声明的路径范围校验。</param>
/// <param name="PathAccess">路径的准入规则，null 表示只执行路径范围边界。</param>
/// <param name="AllowLeadingDash">实参允许以 - 开头，缺省拒绝。</param>
public sealed record ToolParameter(
    ToolName Name,
    string Description,
    bool Flag = false,
    bool Required = false,
    bool List = false,
    bool Path = false,
    ToolPathAccess? PathAccess = null,
    bool AllowLeadingDash = false)
{
    /// <summary>这次调用是否给出了该参数：布尔参数为真、列表参数非空、其余参数取到文本。</summary>
    public bool IsGiven(ToolArguments arguments) => this switch
    {
        { Flag: true } => arguments.Flag(Name) == true,
        { List: true } => (arguments.List(Name) ?? []).Count > 0,
        _ => arguments.Text(Name) is not null,
    };
}