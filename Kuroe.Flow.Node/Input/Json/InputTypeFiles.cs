using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Flow.Node.Input.Json;

/// <summary>输入节点文件形状的注册入口。</summary>
public static class InputTypeFiles
{
    /// <summary>把输入节点的文件形状注册进注册表。</summary>
    public static void RegisterInto(NodeTypeFileRegistry files) => files.Register(new InputTypeFile());
}