using System.Text.Json;

namespace Kuroe.Tools.CommandTools;

/// <summary>命令工具文件的形状：文件里的字段名与类型在此固定，读入后装配成定义。属性必须可写。</summary>
internal sealed class CommandToolFileDto
{
    /// <summary>顶层命令工具定义列表。</summary>
    public List<CommandToolDto> Tools { get; set; } = [];
}

/// <summary>文件里的一个命令工具。</summary>
internal sealed class CommandToolDto
{
    /// <summary>函数名，节点白名单按它匹配。</summary>
    public string? Name { get; set; }

    /// <summary>给模型的说明。</summary>
    public string? Description { get; set; }

    /// <summary>命令行模板。数组的每一项是一个参数项，字符串或带省略声明的对象。</summary>
    public List<JsonElement>? Template { get; set; }

    /// <summary>参数声明。</summary>
    public List<CommandParameterDto>? Parameters { get; set; }

    /// <summary>命令执行目录，相对工作目录；省略时用工作目录根。</summary>
    public string? Directory { get; set; }

    /// <summary>命令超时秒数。</summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>交回文本上限。</summary>
    public int? OutputLimit { get; set; }
}

/// <summary>文件里的一个参数声明。</summary>
internal sealed class CommandParameterDto
{
    /// <summary>参数名，模板占位符按它匹配。</summary>
    public string? Name { get; set; }

    /// <summary>给模型的说明。</summary>
    public string? Description { get; set; }

    /// <summary>模型是否必须给出该参数。</summary>
    public bool Required { get; set; }

    /// <summary>该参数是字符串列表，模型以数组给出。</summary>
    public bool List { get; set; }
}

/// <summary>模板里带省略声明的参数项。Text 是命令行文字，OmitWhenMissing 是关联参数名。</summary>
internal sealed class CommandToolTemplateItemDto
{
    /// <summary>命令行文字，可含 {参数名} 占位符。</summary>
    public string? Text { get; set; }

    /// <summary>关联参数名，该参数缺省时整个参数项消失。</summary>
    public string? OmitWhenMissing { get; set; }
}
