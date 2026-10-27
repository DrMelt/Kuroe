namespace Kuroe.Tool.Json;

/// <summary>文件里配置的工具：定义、配置形态与运行期创建函数。定义与配置形态供列出配置，创建函数按执行建出载体。</summary>
/// <param name="Definition">函数的定义：路径、说明、参数与路径变量。</param>
/// <param name="Configuration">文件里声明的调用形态文本，列出配置时显示。</param>
/// <param name="Create">按工具绑定创建载体。</param>
public sealed record ConfiguredTool(
    ToolDefinition Definition,
    string Configuration,
    Func<ToolBinding, ITool> Create);