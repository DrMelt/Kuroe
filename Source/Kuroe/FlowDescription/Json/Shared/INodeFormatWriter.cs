using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;

namespace Kuroe.FlowDescription.Json.Shared;

/// <summary>节点库文件的写出：把节点定义转回承载格式。</summary>
public interface INodeFormatWriter
{
    /// <summary>写出节点库，失败时给出格式错误。</summary>
    ErrorOr<string> Write(IReadOnlyList<NodeDefinitionDescription> nodes);
}