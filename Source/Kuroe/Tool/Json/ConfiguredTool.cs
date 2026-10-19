namespace Kuroe.Tool.Json;

/// <summary>文件里配置的工具：定义、配置形态与运行期载体。定义与配置形态供列出配置，载体供模型调用。</summary>
/// <param name="Definition">函数的定义：路径、说明与参数。</param>
/// <param name="Configuration">文件里声明的调用形态文本，列出配置时显示。</param>
/// <param name="Tool">运行期载体。</param>
public sealed record ConfiguredTool(ToolDefinition Definition, string Configuration, ITool Tool);