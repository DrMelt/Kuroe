using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;

namespace Kuroe.FlowDescription.Json.Shared;

/// <summary>流程文件的读入：把承载格式转成流程描述，错误按文件位置或流程作用域给出。</summary>
public interface IFlowFormatReader
{
    /// <summary>读入流程文本，形状不合法时给出格式错误。</summary>
    ErrorOr<IReadOnlyList<FlowEntry>> Read(string text);
}