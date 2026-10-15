using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Flow.Node.Input.Json;

/// <summary>输入节点参数段形状的注册入口。</summary>
public static class InputParameterFiles
{
    /// <summary>把输入节点的参数段形状注册进注册表。</summary>
    public static void RegisterInto(NodeParameterFileRegistry files) => files.Register(new InputParameterFile());
}