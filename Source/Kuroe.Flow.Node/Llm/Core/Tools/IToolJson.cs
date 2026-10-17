using System.Text.Json;
using Kuroe.Tool;
using Microsoft.Extensions.AI;

namespace Kuroe.Flow.Node.Llm.Tools;

/// <summary>工具函数的 JSON 承载：参数的 JSON Schema 与一次调用的实参文本。实现由本节点配套的 Json 项目给出，
/// 由宿主在装配处注册。</summary>
public interface IToolJson
{
    /// <summary>参数的 JSON Schema，随请求交给模型。</summary>
    JsonElement Schema(IReadOnlyList<ToolParameter> parameters);

    /// <summary>一次调用的实参写成 JSON 文本，供过程记录。</summary>
    string Describe(AIFunctionArguments arguments);
}