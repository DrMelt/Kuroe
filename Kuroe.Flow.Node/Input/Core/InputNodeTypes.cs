using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Input;

/// <summary>输入节点类型的对外入口：类型名与注册。输入节点不启动 run，用户回答即端口输出。</summary>
public static class InputNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeKind Name { get; } = new("Input");

    /// <summary>把输入类型注册进注册表。</summary>
    public static void RegisterInto(NodeTypeRegistry registry) => registry.Register(new InputNodeType());
}
