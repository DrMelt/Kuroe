using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;

namespace Kuroe.FlowDescription.Json.Shared;

/// <summary>节点库文件的读入：把承载格式转成节点定义，错误按文件位置或节点作用域给出。</summary>
public interface INodeFormatReader
{
    /// <summary>读入节点库文本，形状不合法时给出格式错误。</summary>
    ErrorOr<IReadOnlyList<NodeDefinitionDescription>> Read(string text);
}