using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;

namespace Kuroe.FlowDescription.Json.Shared;

/// <summary>流程文件的写出：把流程描述转回承载格式。</summary>
public interface IFlowFormatWriter
{
    /// <summary>写出流程，失败时给出格式错误。</summary>
    ErrorOr<string> Write(IReadOnlyList<FlowEntry> flows);
}