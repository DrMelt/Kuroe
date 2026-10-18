using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Node.Input.Json;

/// <summary>输入节点文件形状的注册入口。</summary>
public static class InputTypeFiles
{
    /// <summary>把输入节点的文件形状注册进注册表。外部输入协议是读文件建类型实例所需的运行期依赖，由装配处给出。</summary>
    public static void RegisterInto(NodeTypeFileRegistry files, IInputChannel channel) =>
        files.Register(new InputTypeFile(channel));
}