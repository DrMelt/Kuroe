namespace Kuroe.Flow.Json.Parameters;

/// <summary>引擎内置容器的文件形状注册入口。</summary>
public static class ContainerTypeFiles
{
    /// <summary>把容器的文件形状注册进注册表。</summary>
    public static void RegisterInto(NodeTypeFileRegistry files) => files.Register(new ContainerTypeFile());
}