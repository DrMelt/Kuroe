using ErrorOr;
using Kuroe.Flow.Compiler.Shared;

namespace Kuroe.Flow.Json.Shared;

/// <summary>流程文本的读入：把承载格式转成描述文档，错误按文件位置或流程作用域给出。</summary>
public interface IFlowFormatReader
{
    /// <summary>读入文本，形状不合法时给出格式错误。</summary>
    ErrorOr<FlowDocument> Read(string text);
}

/// <summary>流程文本的写出：把描述文档转回承载格式。</summary>
public interface IFlowFormatWriter
{
    /// <summary>写出文档，失败时给出格式错误。</summary>
    ErrorOr<string> Write(FlowDocument document);
}