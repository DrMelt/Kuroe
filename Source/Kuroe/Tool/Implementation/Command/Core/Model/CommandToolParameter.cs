namespace Kuroe.Tool.Command.Model;

/// <summary>命令工具的一个参数声明。名字是模板占位符与关联参数匹配的依据。</summary>
/// <param name="Name">参数名。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Flag">该参数是布尔值，缺省为字符串。</param>
/// <param name="Required">模型是否必须给出，缺省为否。</param>
/// <param name="List">该参数是字符串列表，模型以数组给出；与 <paramref name="Flag"/> 互斥。</param>
/// <param name="Path">该参数是路径，实参按执行目录解析并限制在工作目录内；与 <paramref name="Flag"/> 互斥。</param>
/// <param name="PathAccess">路径的准入规则，null 表示只执行工作目录边界。</param>
public sealed record CommandToolParameter(
    string Name,
    string Description,
    bool Flag = false,
    bool Required = false,
    bool List = false,
    bool Path = false,
    CommandToolPathAccess? PathAccess = null);